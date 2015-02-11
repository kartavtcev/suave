﻿module Susave.Tests.Proxy

#nowarn "25"

open Fuchu

open OpenSSL.X509
open OpenSSL.Core

open System
open System.Net

open Suave
open Suave.Types
open Suave.Http.Successful
open Suave.Http.Redirection
open Suave.Http.ServerErrors
open Suave.Http.Applicatives
open Suave.Http
open Suave.Proxy

open Suave.Tests.TestUtilities
open Suave.Testing

[<Tests>]
let proxy =
  let bind :: _ = defaultConfig.bindings
  let to_target r = Some (bind.socketBinding.ip, bind.socketBinding.port)

  let run_target = runWith defaultConfig

  let run_in_context item f_finally f_body =
    try
      f_body item
    finally
      f_finally item

  // let sslCert = X509Certificate.FromPKCS12(BIO.File("suave.p12","r"), "easy")
  // let proxy_config = { default_config with bindings = [ HttpBinding.Create(Protocol.HTTPS(sslCert), "127.0.0.1", 8084) ] }
  let proxy_config =
    { defaultConfig with
        bindings = [ HttpBinding(HTTP, IPAddress.Loopback, 8084us) ] }
  let proxy = runWithFactory createProxyServerAsync proxy_config

  testList "creating proxy" [
    testPropertyWithConfig fsCheckConfig "GET / returns 200 OK with passed string" <| fun str ->
      run_in_context (run_target (Successful.OK str)) disposeContext <| fun _ ->
        Assert.Equal("target's WebPart should return its value", str,
          proxy to_target |> req HttpMethod.GET "/" None)

    testCase "GET /redirect returns 'redirect'" <| fun _ ->
      run_in_context (run_target (url "/secret" >>= redirect "https://sts.example.se")) disposeContext <| fun _ ->
        let headers, stat =
          proxy to_target |> reqResp HttpMethod.GET "/secret" "" None None DecompressionMethods.None id
            (fun r -> r.Headers, r.StatusCode)
        Assert.Equal("should proxy redirect", HttpStatusCode.Found, stat)
        Assert.Equal("should give Location-header together with redirect",
          Uri("https://sts.example.se"), headers.Location)

    testCase "Should proxy 500 Internal Server Error too" <| fun _ ->
      run_in_context (run_target (INTERNAL_ERROR "Oh noes")) disposeContext <| fun _ ->
        Assert.Equal("should have correct status code",
          HttpStatusCode.InternalServerError,
          proxy to_target |> reqResp HttpMethod.GET "/" "" None None DecompressionMethods.None id status_code)
        Assert.Equal("should have correct content",
          "Oh noes",
          proxy to_target |> reqResp HttpMethod.GET "/" "" None None DecompressionMethods.None id content_string)

    testCase "Proxy decides to return directly" <| fun _ ->
      run_in_context (run_target (OK "upstream reply")) disposeContext <| fun _ ->
//          let subject = proxy (choose [ 
        Assert.Equal("", true, true)
    ]
