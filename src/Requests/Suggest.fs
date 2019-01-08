module HCup.Requests.Suggest

open HCup.Dictionaries
open HCup
open HCup.Common
open Microsoft.AspNetCore.Http
open BitmapIndex
open System
open System.Collections.Generic
open System.Threading
open HCup.MethodCounter
open HCup.Filters
open HCup.BufferSerializers
open HCup.Requests.Filter
open Giraffe
open System.IO
open HCup.Models
open HCup.Requests

let getSimilarityNew targetId (likers: SortedList<int,struct(single*byte)>) (results:Dictionary<int,single>) =
    let struct(targTsSum, count) = likers.[targetId]
    let targTs = targTsSum / (single) count
    for liker in likers do
        if liker.Key <> targetId
        then
            let struct(likerTsSum, count) = liker.Value
            let likerTs = likerTsSum / (single) count
            let currrentSimilarity = 1.0f / Math.Abs(targTs - likerTs)
            if (results.ContainsKey(liker.Key))
            then results.[liker.Key] <- results.[liker.Key] + currrentSimilarity
            else results.[liker.Key] <- currrentSimilarity

let suggestionFields = [| "status_eq"; "fname_eq"; "sname_eq"; |]
let getSuggestedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsSuggestCount) |> ignore
    try
    if (id > accountsNumber || (box accounts.[id] |> isNull) )
    then
        setStatusCode 404 next ctx
    else
        let target = accounts.[id]
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
            |> Seq.sortBy (fun key -> groupFiltersOrder.[key])
            |> Seq.map (fun key ->
                    let value = ctx.Request.Query.[key].[0]
                    if String.IsNullOrEmpty(value)
                    then raise (KeyNotFoundException("Unknown value of get parameter"))
                    (key, value)
                )
            |> Seq.toArray
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        if limit <= 0 || limit > 20
        then
            setStatusCode 400 next ctx
        else
            if target.likes |> isNull
            then
                let memoryStream = serializeAccounts ([||], Recommend.recommendationFields)
                writeResponse memoryStream next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let similaritiesWithUsers = Dictionary<int, single>()
                for likeId in target.likes do
                    getSimilarityNew target.id (likesIndex.[likeId]) similaritiesWithUsers
                let similarAccounts =
                    similaritiesWithUsers.Keys
                    |> Seq.map (fun id -> accounts.[id])
                    |> Seq.filter(fun acc -> acc.sex = target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) similarAccounts
                    |> Seq.sortByDescending (fun acc -> similaritiesWithUsers.[acc.id])
                    |> Seq.collect(fun acc -> acc.likes)
                    |> Seq.filter (fun likeId -> target.likes.Contains(likeId) |> not)
                    |> Seq.map (fun likeId -> accounts.[likeId])
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, suggestionFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx
