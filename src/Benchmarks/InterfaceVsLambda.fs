namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.IO
open System.Runtime.CompilerServices


[<Interface>]
type ILogger =
     abstract Info: string -> unit

type LogClass() =
    member this.Info0 (i: string) =
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)

    interface ILogger with
        member this.Info i =
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)

module LogRec2 =
    let log2 (i: string) =
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)
        TextWriter.Null.WriteLine(i)

type LogRec =
    {
        Info: string -> unit
    }

//[<MemoryDiagnoser>]
type RecordVsLambda() =
    
    let text = "abcd"
    let classExample = LogClass()
    let interfaceExample = LogClass() :> ILogger
    let recordExample = 
        { Info = fun i ->
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)
            TextWriter.Null.WriteLine(i)        }

    let recordExample2 = 
        { Info = LogRec2.log2  }
    
    [<Benchmark(Baseline=true)>]
    member _.InterfaceExample() = 
        interfaceExample.Info text
        interfaceExample.Info text
        interfaceExample.Info text

    [<Benchmark>]
    member _.RecordExample() = 
        recordExample.Info text
        recordExample.Info text
        recordExample.Info text

    [<Benchmark>]
    member _.RecordExample2() = 
        recordExample2.Info text
        recordExample2.Info text
        recordExample2.Info text

    [<Benchmark>]
    member _.ClassExample() = 
        classExample.Info0 text
        classExample.Info0 text
        classExample.Info0 text

    [<Benchmark>]
    member _.ByHand() = 
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)
        TextWriter.Null.WriteLine(text)