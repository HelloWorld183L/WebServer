module HttpHandler

open FileScraper
open MimeTypes
open NetworkingTypes
open System
open System.Text

let crlf = "\r\n"

let setUpHttpHeaders contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type: " + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none"
    
let parseHttpRequest (httpRequest : string) =
    let requestParts = httpRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let httpRequestInfo = { RequestMethod = requestParts.[0]; ResourceName = requestParts.[1] }
    httpRequestInfo

let getHttpHeaders resourceName =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders
    httpHeaders + crlf + crlf

let assembleSendBuffer resourceName =
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
    byteData