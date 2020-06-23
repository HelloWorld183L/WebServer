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
        mutable SendBuffer: byte[]
        mutable ReceiveBuffer: byte[]
    }

let defaultBufferSize = 10240
let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
let crlf = "\r\n"

let sendCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> Client
    try
        clientSocketState.Socket.EndSend(result)
        |> printfn "Sent %i bytes to client"
    with :? Exception as ex -> 
        printfn "%s" ex.Message

let setUpHttpHeaders (contentLength : int) contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type: " + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none"

let getHttpHeaders resourceName contentLength =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders contentLength
    httpHeaders + crlf + crlf

let reply (clientSocket : Client) resourceName =
    let extraSpace =
        crlf + crlf
        |> Encoding.UTF8.GetBytes

    let contentByteData =
        resourceName
        |> getResourcePath
        |> getFileContents
    
    let length = (contentByteData.Length + extraSpace.Length)
    let contentByteDataWithSpace = Array.zeroCreate length : byte[]
    Array.Copy(contentByteData, contentByteData.GetLowerBound(0), contentByteDataWithSpace, contentByteData.GetUpperBound(0) - 1, contentByteData.Length)
    Array.Copy(extraSpace, extraSpace.GetLowerBound(0), contentByteDataWithSpace, contentByteDataWithSpace.GetUpperBound(0) - 1, extraSpace.Length)

    let headerByteData =
        contentByteData.Length
        |> getHttpHeaders resourceName
        |> Encoding.UTF8.GetBytes 
    Array.Copy(extraSpace, 0, headerByteData, headerByteData.Length - 1, extraSpace.Length)

    let combinedLength = (contentByteData.Length + headerByteData.Length)
    let byteData = Array.zeroCreate combinedLength
    Array.Copy(headerByteData, 0, byteData, byteData.Length - 1, headerByteData.Length)
    Array.Copy(contentByteData, 0, byteData, byteData.Length - 1, contentByteData.Length)

    clientSocket.SendBuffer <- byteData
    clientSocket.Socket.BeginSend(clientSocket.SendBuffer, 0, clientSocket.SendBuffer.Length, SocketFlags.None,
                                  new AsyncCallback(sendCallback), clientSocket)

let parsePacketInfo (httpRequest : string) =
    let requestParts = httpRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let httpRequestInfo = { RequestMethod = requestParts.[0]; ResourceName = requestParts.[1] }
    httpRequestInfo

let rec readCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> Client
    let clientSocket = clientSocketState.Socket
    
    let bufferSize, socketError = clientSocket.EndReceive(result)
    if bufferSize <> 0 && socketError = SocketError.Success then
        if socketError <> SocketError.Success then 
            printfn "CLIENT SOCKET ERROR! Error: %s" (socketError.ToString())

        let packet = Array.zeroCreate bufferSize
        Array.Copy(clientSocketState.ReceiveBuffer, packet, bufferSize)

        let httpRequestInfo =
            Encoding.UTF8.GetString packet
            |> parsePacketInfo

        httpRequestInfo.ResourceName
        |> reply clientSocketState

        clientSocketState.SendBuffer <- Array.zeroCreate 0
        clientSocketState.ReceiveBuffer <- Array.zeroCreate 0
        clientSocketState.Socket.Dispose()
        ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    let clientSocketState = {Socket=clientSocket; ReceiveBuffer=Array.zeroCreate defaultBufferSize; SendBuffer=Array.zeroCreate 30000}

    clientSocket.BeginReceive(clientSocketState.ReceiveBuffer, 0, clientSocketState.ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocketState)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")