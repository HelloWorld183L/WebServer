module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open NetworkingTypes
open HttpHandler
open SocketHandler

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

let sendResponse clientSocket resourceName =
    let byteData = assembleSendBuffer resourceName

    clientSocket.SendBuffer <- byteData
    clientSocket.Socket.BeginSend(clientSocket.SendBuffer, 0, clientSocket.SendBuffer.Length, SocketFlags.None, new AsyncCallback(sendCallback), clientSocket)

let rec readCallback (result : IAsyncResult) =
    let clientSocketState = result.AsyncState :?> Client
    let clientSocket = clientSocketState.Socket
    
    let bufferSize, socketError = clientSocket.EndReceive(result)
    if bufferSize <> 0 then
        if socketError <> SocketError.Success then 
            printfn "CLIENT SOCKET ERROR! Error: %s" (socketError.ToString())

        let packet = Array.zeroCreate bufferSize
        Array.Copy(clientSocketState.ReceiveBuffer, packet, bufferSize)

        let httpRequestInfo =
            packet
            |> Encoding.UTF8.GetString
            |> parseHttpRequest

        httpRequestInfo.ResourceName
        |> sendResponse clientSocketState

        disconnect clientSocketState

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