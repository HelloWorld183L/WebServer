module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open FileScraper

type StateObject() =
    member this.WorkSocket : Socket = null
    member this.BufferSize = 1024
    member this.Buffer : byte [] = Array.zeroCreate 1024
    member this.StringBuilder = new StringBuilder()

type HttpRequest =
    {
        RequestMethod: string
        ResourcePath: string
    }

let buffer : byte [] = Array.zeroCreate 1024
let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

let sendCallback (result : IAsyncResult) =
    try
        let handler = result.AsyncState :?> Socket

        let bytesSent = handler.EndSend(result)
        printfn "Sent %i bytes to client" bytesSent

        handler.Shutdown(SocketShutdown.Both)
        handler.Close()
    with
        :? Exception as ex -> printfn "%s" (ex.ToString())

let reply (fileContents : string) =
    let httpHeader = 
    "HTTP/1.1 200 OK
    Server: CustomWebServer 1.0
    Content-Type: text/html; charset=utf-8
    Accept-Ranges: none"
    
    let byteData = Encoding.UTF8.GetBytes(fileContents)

    serverSocket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
        new AsyncCallback(sendCallback), serverSocket)

let rec readCallback (result : IAsyncResult) =
    let clientSocket = result.AsyncState :?> Socket

    let bufferSize, socketError = clientSocket.EndReceive(result)
    if socketError <> SocketError.Success then printfn "SOCKET ERROR! Error: %s" (socketError.ToString())

    let packet = Array.zeroCreate bufferSize
    Array.Copy(buffer, packet, packet.Length)

    let packetInfo = Encoding.ASCII.GetString(packet)
    let httpRequest = { RequestMethod=packetInfo.Substring(0, 3); ResourcePath=packetInfo.Substring(4, 11)}
    
    let resourcePath =
        if String.IsNullOrEmpty(httpRequest.ResourcePath) then
            getResourcePath "index.html"
        else
            httpRequest.ResourcePath.Replace('/','\\')
            |> getResourcePath

    resourcePath
    |> getFileContents
    |> reply

    buffer = Array.zeroCreate 1024
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    buffer = Array.zeroCreate 1024

    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocket)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")

