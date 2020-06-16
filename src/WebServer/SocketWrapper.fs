module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text

type StateObject() =
    member this.WorkSocket : Socket = null
    member this.BufferSize = 1024
    member this.Buffer : byte [] = [||]
    member this.StringBuilder = new StringBuilder()

let serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

let rec readCallback (result : IAsyncResult) =
    let state = (StateObject) result.AsyncState
    let handler = state.WorkSocket

    let read = handler.EndReceive(result)
    if read > 0 then
        state.StringBuilder.Append(Encoding.ASCII.GetString(state.Buffer, 0, read))
        handler.BeginReceive(state.Buffer, 0, state.BufferSize, SocketFlags.None, new AsyncCallback(readCallback), state)
        ()
    else
        if state.StringBuilder.Length > 1 then
            let content = state.StringBuilder.ToString()
            Console.WriteLine "Read %i bytes from socket.\n Data : %s" content.Length content
        handler.Close()

let rec setupSocket =
    let clientSockets = []

    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(10)
    serverSocket.BeginAccept(new AsyncCallback(readCallback), serverSocket)