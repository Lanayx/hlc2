module HCup.Common

open System
open BitmapIndex
open System.Collections.Generic
open HCup.Models
open Utf8Json

let timestampBase = DateTime(1970, 1, 1)
[<Literal>]
let secondsInYear = 31556926

let inline convertToDate timestamp =
    timestampBase.AddSeconds((float)timestamp)

let inline convertToTimestamp (date: DateTime) =
    (int)(date - timestampBase).TotalSeconds

let mutable currentTs = 0

let inline (=~) str1 str2 =
    String.Equals(str1, str2,StringComparison.Ordinal)

let inline (==) (str1: string) str2 =
    MemoryExtensions.Equals(str1.AsSpan(), str2, StringComparison.Ordinal)


[<Literal>]
let freeStatus = 2uy
[<Literal>]
let complexStatus = 0uy
[<Literal>]
let occupiedStatus = 1uy

[<Literal>]
let female = 0uy
[<Literal>]
let male = 1uy

[<Literal>]
let routeName = "routeName"
[<Literal>]
let filterRoute = "filter"
[<Literal>]
let groupRoute = "group"
[<Literal>]
let recommendRoute = "recommend"
[<Literal>]
let suggestRoute = "suggest"
[<Literal>]
let newAccountRoute = "newAccount"
[<Literal>]
let updateAccountRoute = "updateAccount"
[<Literal>]
let addLikesRoute = "addLikes"

type IntReverseComparer() =
    interface IComparer<int> with
        member this.Compare(x,y) =
            if x > y
            then -1
            else
                if x < y
                then 1
                else 0
let intReverseComparer = new IntReverseComparer()

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let accounts: Account[] = Array.zeroCreate(1400000)
let mutable accountsNumber = 0
let mutable interestsIndex = BitmapIndex()

let inline getRevAccounts() =
    seq {
        for i = accountsNumber downto 1 do
            yield accounts.[i]
    }

let inline getAccounts() =
    accounts
    |> Seq.skip 1
    |> Seq.take accountsNumber

type LikeReverseComparer() =
    interface IComparer<SmartLike> with
        member this.Compare(x,y) =
            if x.likee > y.likee
            then -1
            else
                if x.likee < y.likee
                then 1
                else 0
let likeReverseComparer = new LikeReverseComparer()


let inline findLikeIndex2 (likes: ResizeArray<SmartLike>) likeToSearch =
    likes.BinarySearch(likeToSearch, likeReverseComparer)

let inline findLikeIndex (likes: ResizeArray<SmartLike>) likee =
    let likeToSearch = { likee = likee; sumOfTs = 0.0f; tsCount = 1uy }
    findLikeIndex2 likes likeToSearch

let inline seqBack (array: ResizeArray<'T>) =
    seq {
        for i = array.Count-1 downto 0 do  
            yield array.[i]
    }
