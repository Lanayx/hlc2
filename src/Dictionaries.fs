module HCup.Dictionaries

open System.Collections.Generic
open HCup.Models

let namesDictionary = Dictionary<string, int64>()
let snamesDictionary = Dictionary<string, int64>()
let citiesDictionary = Dictionary<string, int64>()
let countriesDictionary = Dictionary<string, int64>()
let interestsDictionary = Dictionary<string, int64>()

let mutable namesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable snamesSerializeDictionary = Dictionary<int64, struct(string*byte[])>()
let mutable citiesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable countriesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable interestsSerializeDictionary = Dictionary<int64, byte[]>()

let likesDictionary = Dictionary<int, SortedDictionary<int, struct(single*int)>>()
let emailsDictionary = HashSet<string>()
