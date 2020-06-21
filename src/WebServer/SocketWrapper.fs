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

type SocketStateObject =
    {
        Socket: Socket
        mutable IsDead: bool
    }

let buffer : byte [] = Array.zeroCreate 1024
let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
let serverSocketState = { Socket=serverSocket; IsDead=false}
let crlf = "\r\n"

let socketIsConnected (clientSocketState : SocketStateObject) =
    if clientSocketState.Socket.Connected = false then
        printfn "Client socket is NOT connected."
        clientSocketState.IsDead <- true
    clientSocketState.Socket.Connected

let sendCallback (result : IAsyncResult) =
    let clientSocket = result.AsyncState :?> SocketStateObject
    try
        let bytesSent = clientSocket.Socket.EndSend(result)
        printfn "Sent %i bytes to client" bytesSent
    with :? Exception as ex -> 
        printfn "%s" ex.Message
    
    clientSocket.IsDead <- true
    clientSocket.Socket.Dispose()

let setUpHttpHeaders contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type:" + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none" + crlf

let getHttpMessage resourceName fileContents =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders
    httpHeaders + crlf + crlf + fileContents + crlf + crlf

let reply (clientSocket : SocketStateObject) httpRequestInfo =
    let byteData =
        httpRequestInfo.ResourceName
        |> getResourcePath
        |> getFileContents
        |> getHttpMessage httpRequestInfo.ResourceName
        |> Encoding.UTF8.GetBytes 
    
    clientSocket.Socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
        new AsyncCallback(sendCallback), clientSocket)

let handlePacket packet =
    let convertToUInt16 index = BitConverter.ToUInt16(packet, index)

    let packetLength = convertToUInt16 0
    let packetType = convertToUInt16 2

    printfn "Received packet! Length: %i | Type: %i" packetLength packetType

let rec readCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> SocketStateObject
    let clientSocket = clientSocketState.Socket
    
    let bufferSize, socketError = clientSocket.EndReceive(result)
    clientSocketState.IsDead <- socketIsConnected clientSocketState
    if clientSocketState.IsDead = false then
        if socketError <> SocketError.Success then 
            printfn "CLIENT SOCKET ERROR! Error: %s" (socketError.ToString())
            clientSocketState.IsDead <- true

        let packet = Array.zeroCreate bufferSize
        Array.Copy(buffer, packet, packet.Length)
        handlePacket packet

        let packetInfo = Encoding.ASCII.GetString(packet)
        { RequestMethod=packetInfo.Substring(0, 3); ResourceName=packetInfo.Substring(4, 11)}
        |> reply clientSocketState

        clientSocketState.IsDead <- socketIsConnected clientSocketState
        if clientSocketState.IsDead = false then
            buffer = Array.zeroCreate 1024
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
            new AsyncCallback(readCallback), clientSocketState)
            ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    let clientSocketState = {Socket=clientSocket; IsDead=false}

    buffer = Array.zeroCreate 1024

    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocketState)
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback),
                                clientSocketState)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")