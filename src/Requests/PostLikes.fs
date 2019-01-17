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
                    let smartLike = { likee = like.likee; sumOfTs = (single)like.ts; tsCount = 1uy }
                    if (acc.likes |> isNull)
                    then acc.likes <- ResizeArray(seq { yield smartLike })
                    else
                        let likeIndex = findLikeIndex acc.likes like.likee
                        if (likeIndex > 0)
                        then
                            let existingLike = acc.likes.[likeIndex]
                            acc.likes.[likeIndex] <- { existingLike with sumOfTs = existingLike.sumOfTs + (single) like.ts; tsCount = existingLike.tsCount + 1uy  }
                        else
                            acc.likes.Add(smartLike)
                            acc.likes.Sort(likeReverseComparer)
                    addLikeToDictionary like.liker like.likee
                return! writePostResponse 202 next ctx
        with
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }
