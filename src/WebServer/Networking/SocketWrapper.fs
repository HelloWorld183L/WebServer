module SocketWrapper
open System
open System.Net
open System.Net.Sockets
open System.Text
open NetworkingTypes
open HttpHandler
open SocketHandler

let defaultBufferSize = 1024
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
    clientSocket.Socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(sendCallback), clientSocket)

let rec readCallback (result : IAsyncResult) =
    let client = result.AsyncState :?> Client
    let clientSocket = client.Socket
    
    let bufferSize, socketError = clientSocket.EndReceive(result)
    if bufferSize <> 0 then
        if socketError <> SocketError.Success then 
            handleSocketError client socketError

        let packet = Array.zeroCreate bufferSize
        Array.Copy(client.ReceiveBuffer, packet, bufferSize)

        let httpRequestInfo =
            packet
            |> Encoding.UTF8.GetString
            |> parseHttpRequest

        httpRequestInfo.ResourceName
        |> sendResponse client

        disconnect client

let rec acceptCallback (result : IAsyncResult) =
    let clientSocket = serverSocket.EndAccept(result)
    let clientSocketState = {Socket=clientSocket; ReceiveBuffer=Array.zeroCreate defaultBufferSize; }

    clientSocket.BeginReceive(clientSocketState.ReceiveBuffer, 0, clientSocketState.ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(readCallback), clientSocketState)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    ()

let rec startListening() =
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, 80))
    serverSocket.Listen(500)
    serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null)
    
    while true do Console.ReadLine()
    
    Console.WriteLine("Closing the listener...")