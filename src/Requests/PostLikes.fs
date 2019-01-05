module HCup.Requests.PostLikes

open Microsoft.AspNetCore.Http
open System.Threading
open FSharp.Control.Tasks.V2.ContextInsensitive
open HCup.MethodCounter
open HCup.Common
open HCup.Models
open Giraffe
open HCup.Requests.PostAccount
open HCup.BufferSerializers
open Utf8Json

let addLikes (next, ctx : HttpContext) =
    Interlocked.Increment(addLikesCount) |> ignore
    task {
        try
            let! json = ctx.ReadBodyFromRequestAsync()
            let likes = deserializeObjectUnsafe<LikesUpd>(json)
            let wrongLike =
                likes.likes
                |> Array.tryFind(fun like ->
                    box accounts.[like.liker] |> isNull || box accounts.[like.likee] |> isNull)
            match wrongLike with
            | Some _ -> return! setStatusCode 400 next ctx
            | None ->
                for like in likes.likes do
                    let acc = accounts.[like.liker]
                    if (acc.likes |> isNull)
                    then acc.likes <- ResizeArray(seq { yield like.likee })
                    else
                        if (acc.likes.Contains(like.likee) |> not)
                        then
                            acc.likes.Add(like.likee)
                            acc.likes.Sort(intReverseComparer)
                    addLikeToDictionary like.liker like.likee like.ts
                return! writePostResponse 202 next ctx
        with
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }
