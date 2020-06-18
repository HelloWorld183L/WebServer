module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open System.IO
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

let reply (handler : Socket) (data : string) =
    let byteData = Encoding.ASCII.GetBytes(data)

    handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
        new AsyncCallback(sendCallback), handler)

let rec readCallback (result : IAsyncResult) =
    let clientSocket = result.AsyncState :?> Socket

    let bufferSize = clientSocket.EndReceive(result)
    let packet = Array.zeroCreate bufferSize
    Array.Copy(buffer, packet, packet.Length)

    let packetInfo = Encoding.ASCII.GetString(packet)
    let httpRequest = { RequestMethod=packetInfo.Substring(0, 3); ResourcePath=packetInfo.Substring(4, 11)}
    
    let resourcePath =
        if String.IsNullOrEmpty(httpRequest.ResourcePath) then
            getResourcePath "/index.html"
        else
            getResourcePath httpRequest.ResourcePath
    let fileContents = getFileContents resourcePath
    reply serverSocket fileContents

    buffer = Array.zeroCreate 1024
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    ()

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    buffer = Array.zeroCreate 1024

    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocket)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")

