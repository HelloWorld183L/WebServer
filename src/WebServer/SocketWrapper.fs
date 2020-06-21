module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open FileScraper
open MimeTypes

type HttpRequestInfo =
    {
        RequestMethod: string
        ResourceName: string
    }

type Client =
    {
        Socket: Socket
        SendBuffer: byte[]
        ReceiveBuffer: byte[]
    }

let defaultBufferSize = 10240
let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
let crlf = "\r\n"

let sendCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> Client
    try
        let bytesSent = clientSocketState.Socket.EndSend(result)
        printfn "Sent %i bytes to client" bytesSent
    with :? Exception as ex -> 
        printfn "%s" ex.Message
    
    clientSocketState.SendBuffer = Array.zeroCreate 0
    clientSocketState.ReceiveBuffer = Array.zeroCreate 0
    clientSocketState.Socket.Dispose()

let setUpHttpHeaders (contentLength : int) contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type:" + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none" + crlf +
    "Content-Length: " + (contentLength.ToString())

let getHttpMessage resourceName (fileContents : string) =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders fileContents.Length
    httpHeaders + crlf + crlf + fileContents + crlf + crlf

let reply (clientSocket : Client) resourceName =
    let byteData =
        resourceName
        |> getResourcePath
        |> getFileContents
        |> getHttpMessage resourceName
        |> Encoding.UTF8.GetBytes 
    
    clientSocket.Socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
                                  new AsyncCallback(sendCallback), clientSocket)

let rec readCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> Client
    let clientSocket = clientSocketState.Socket
    
    let bufferSize, socketError = clientSocket.EndReceive(result)
    if bufferSize <> 0 && socketError = SocketError.Success = false then
        if socketError <> SocketError.Success then 
            printfn "CLIENT SOCKET ERROR! Error: %s" (socketError.ToString())

        let packet = Array.zeroCreate bufferSize
        Array.Copy(clientSocketState.Buffer, packet, bufferSize)

        let packetInfo = Encoding.UTF8.GetString(packet)
        let httpRequestInfo = { RequestMethod=packetInfo.Substring(0, 3); 
                                ResourceName=packetInfo.Substring(4, 11)}
        httpRequestInfo.ResourceName
        |> reply clientSocketState

        if socketError = SocketError.Success then
            clientSocketState.Buffer = Array.zeroCreate 10240
            clientSocket.BeginReceive(clientSocketState.Buffer, 0, clientSocketState.Buffer.Length, SocketFlags.None,
            new AsyncCallback(readCallback), clientSocketState)
            clientSocketState.Socket.Dispose()
            ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    let clientSocketState = {Socket=clientSocket; ReceiveBuffer=Array.zeroCreate defaultBufferSize; SendBuffer=null}

    clientSocket.BeginReceive(clientSocketState.ReceiveBuffer, 0, clientSocketState.ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(readCallback),
                              clientSocketState)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")