module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text

type StateObject() =
    member this.WorkSocket : Socket = null
    member this.BufferSize = 1024
    member this.Buffer : byte [] = Array.zeroCreate 1024
    member this.StringBuilder = new StringBuilder()

let buffer : byte [] = Array.zeroCreate 1024

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
    let clientSocket = result.AsyncState :?> StateObject
    let handler = clientSocket.WorkSocket

    let bufferSize = handler.EndReceive(result)
    let packet = Array.zeroCreate bufferSize
    Array.Copy(buffer, packet, packet.Length)

    if bufferSize > 0 then
        clientSocket.StringBuilder.Append(Encoding.ASCII.GetString(clientSocket.Buffer, 0, bufferSize))
        handler.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
        ()
    else
        if clientSocket.StringBuilder.Length > 1 then
            let content = clientSocket.StringBuilder.ToString()
            printfn "Read %i bytes from socket.\n Data : %s" content.Length content
        handler.Close()

let acceptCallback (result : IAsyncResult) =
    let listener = result.AsyncState :?> Socket
    let clientSocket = listener.EndAccept(result)
    buffer = Array.zeroCreate 1024

    let state = new StateObject()
    state.WorkSocket = clientSocket
    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocket)
    ()

let rec startListening() =
    let listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    listener.Bind(new IPEndPoint(IPAddress.Any, 80))
    listener.Listen(500)
    listener.BeginAccept(new AsyncCallback(acceptCallback), listener)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")

