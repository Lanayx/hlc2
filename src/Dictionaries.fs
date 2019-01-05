module HCup.Dictionaries

open System.Collections.Generic
open HCup.Models

let fnamesWeightDictionary = Dictionary<string, int64>()
let snamesWeightDictionary = Dictionary<string, int64>()
let citiesWeightDictionary = Dictionary<string, int64>()
let countriesWeightDictionary = Dictionary<string, int64>()
let interestsWeightDictionary = Dictionary<string, int64>()

let mutable namesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable snamesSerializeDictionary = Dictionary<int64, struct(string*byte[])>()
let mutable citiesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable countriesSerializeDictionary = Dictionary<int64, byte[]>()
let mutable interestsSerializeDictionary = Dictionary<int64, byte[]>()

// key is likeId,  value is dict of key=userId, value=sumOfTs*tsCount
let likesIndex = Dictionary<int, SortedDictionary<int, struct(single*int)>>()
// key is cityWeight, value is usersId's set
let citiesIndex = Dictionary<int64, SortedSet<int>>()
let countriesIndex = Dictionary<int64, SortedSet<int>>()
let fnamesIndex = Dictionary<int64, SortedSet<int>>()
let emailsDictionary = HashSet<string>()

// key is sex, value is dict of key=city, value=Count
let mutable citySexGroups = Dictionary<char, Dictionary<int64,int>>()
citySexGroups.['f'] <- Dictionary<int64,int>()
citySexGroups.['m'] <- Dictionary<int64,int>()
let mutable cityStatusGroups = Dictionary<int, Dictionary<int64,int>>()
cityStatusGroups.[0] <- Dictionary<int64,int>()
cityStatusGroups.[1] <- Dictionary<int64,int>()
cityStatusGroups.[2] <- Dictionary<int64,int>()
let mutable countrySexGroups = Dictionary<char, Dictionary<int64,int>>()
countrySexGroups.['f'] <- Dictionary<int64,int>()
countrySexGroups.['m'] <- Dictionary<int64,int>()
let mutable countryStatusGroups = Dictionary<int, Dictionary<int64,int>>()
countryStatusGroups.[0] <- Dictionary<int64,int>()
countryStatusGroups.[1] <- Dictionary<int64,int>()
countryStatusGroups.[2] <- Dictionary<int64,int>()

// key is interest value is Count
let mutable interestGroups = Dictionary<int64,int>()