// Learn more about F# at http://fsharp.org

open System
open BenchmarkDotNet.Running
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open Benchmarks





[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<JsonElementBenchmarks>()
    0
