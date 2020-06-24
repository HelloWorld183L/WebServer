module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open FileScraper
open MimeTypes
open NetworkingTypes

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

let setUpHttpHeaders contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type: " + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none"

let getHttpHeaders resourceName =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders
    httpHeaders + crlf + crlf

let reply (clientSocket : Client) resourceName =
    let extraSpace =
        crlf + crlf
        |> Encoding.UTF8.GetBytes

    let contentByteData =
        resourceName
        |> getResourcePath
        |> getFileContents
    
    let contentLength = (contentByteData.Length + extraSpace.Length)
    let contentByteDataWithSpace = Array.zeroCreate contentLength : byte[]
    contentByteData.CopyTo(contentByteDataWithSpace, 0)
    extraSpace.CopyTo(contentByteDataWithSpace, contentByteData.Length - 1)

    let headerByteData =
        resourceName
        |> getHttpHeaders
        |> Encoding.UTF8.GetBytes
    let headerLength = (headerByteData.Length + extraSpace.Length)
    let headerByteDataWithSpace = Array.zeroCreate headerLength : byte[]
    headerByteData.CopyTo(headerByteDataWithSpace, 0)
    extraSpace.CopyTo(headerByteDataWithSpace, headerByteData.Length - 1)

    let combinedLength = (contentByteDataWithSpace.Length + headerByteDataWithSpace.Length)
    let byteData = Array.zeroCreate combinedLength
    contentByteDataWithSpace.CopyTo(byteData, 0)
    headerByteDataWithSpace.CopyTo(byteData, contentByteData.Length - 1)

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