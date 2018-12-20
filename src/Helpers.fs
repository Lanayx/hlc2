module HCup.Helpers

open System

let timestampBase = DateTime(1970, 1, 1, 0, 0, 0, 0)
[<Literal>]
let secondsInYear = 31556926

let inline convertToDate timestamp =
    timestampBase.AddSeconds((float)timestamp)

let inline convertToTimestamp (date: DateTime) =
    (int)(date - timestampBase).TotalSeconds

let mutable currentTs = 0

let inline (=~) str1 str2 =
    String.Equals(str1, str2,StringComparison.Ordinal)

