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

let buffer : byte [] = Array.zeroCreate 1024
let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

let socketIsConnected (socket : Socket) =
    if socket.Connected <> true then
        printfn "Socket is NOT connected."
    socket.Connected

let sendCallback (result : IAsyncResult) =
    let clientSocket = result.AsyncState :?> Socket

    let bytesSent = clientSocket.EndSend(result)
    printfn "Sent %i bytes to client" bytesSent

    clientSocket.Dispose()

let reply httpRequestInfo (clientSocket : Socket) =
    let fileInfo = getFileInfo httpRequestInfo.ResourceName

    let crlf = "\r\n"
    let contentType = getMimeType fileInfo
    let httpHeaders = 
        "HTTP/1.1 200 OK" + crlf +
        "Server: CustomWebServer 1.0" + crlf +
        "Content-Type:" + contentType + "; charset=utf-8" + crlf +
        "Accept-Ranges: none" + crlf
    
    let fileContents =
        getResourcePath httpRequestInfo.ResourceName
        |> getFileContents
    let byteData =
        httpHeaders + crlf + crlf + fileContents + crlf + crlf
        |> Encoding.UTF8.GetBytes

    clientSocket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
        new AsyncCallback(sendCallback), clientSocket)

let handlePacket packet =
    let convertToUInt16 packet index = BitConverter.ToUInt16(packet, index)

    let getPacketDetail = convertToUInt16 packet
    let packetLength = getPacketDetail 0
    let packetType = getPacketDetail 2

    printfn "Received packet! Length: %i | Type: %i" packetLength packetType

let rec readCallback (result : IAsyncResult) =
    let clientSocket = result.AsyncState :?> Socket

    let bufferSize, socketError = clientSocket.EndReceive(result)
    if socketError <> SocketError.Success then printfn "SOCKET ERROR! Error: %s" (socketError.ToString())

    let packet = Array.zeroCreate bufferSize
    Array.Copy(buffer, packet, packet.Length)
    handlePacket packet

    let packetInfo = Encoding.ASCII.GetString(packet)
    let httpRequest = { RequestMethod=packetInfo.Substring(0, 3); ResourceName=packetInfo.Substring(4, 11)}

    reply httpRequest clientSocket

    if socketIsConnected clientSocket then
        buffer = Array.zeroCreate 1024
        clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
        new AsyncCallback(readCallback), clientSocket)
        ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    buffer = Array.zeroCreate 1024

    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocket)
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocket)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")