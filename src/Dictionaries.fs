module HCup.Dictionaries

open System.Collections.Generic
open HCup.Models
open System

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
let likesIndex: SortedList<int, struct(single*byte)>[] = Array.zeroCreate(1400000)
// key is cityWeight, value is usersId's set
let citiesIndex = Dictionary<int64, SortedSet<int>>()
let countriesIndex = Dictionary<int64, SortedSet<int>>()
let fnamesIndex = Dictionary<int64, SortedSet<int>>()
let emailsDictionary = HashSet<string>()

// key is sex, value is dict of key=city, value=Count
let mutable citySexGroups = Dictionary<byte, Dictionary<int64,int>>()
citySexGroups.[0uy] <- Dictionary<int64,int>()
citySexGroups.[1uy] <- Dictionary<int64,int>()
let mutable cityStatusGroups = Dictionary<byte, Dictionary<int64,int>>()
cityStatusGroups.[0uy] <- Dictionary<int64,int>()
cityStatusGroups.[1uy] <- Dictionary<int64,int>()
cityStatusGroups.[2uy] <- Dictionary<int64,int>()
let mutable countrySexGroups = Dictionary<byte, Dictionary<int64,int>>()
countrySexGroups.[0uy] <- Dictionary<int64,int>()
countrySexGroups.[1uy] <- Dictionary<int64,int>()
let mutable countryStatusGroups = Dictionary<byte, Dictionary<int64,int>>()
countryStatusGroups.[0uy] <- Dictionary<int64,int>()
countryStatusGroups.[1uy] <- Dictionary<int64,int>()
countryStatusGroups.[2uy] <- Dictionary<int64,int>()

// key is interest value is Count
let mutable interestGroups = Dictionary<int64,int>()
type BirthGroup = int16
type JoinedGroup = int16
type SexGroup = byte
type StatusGroup = byte
type CityGroup = int64
type CountryGroup = int64
type InterestsGroup = int64
type CountType = int

let allCountryGroups =
    Dictionary<CountryGroup,struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allCityGroups =
    Dictionary<CityGroup,struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allSexGroups =
    Dictionary<SexGroup,struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,CountType>*Dictionary<CountryGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allStatusGroups =
    Dictionary<StatusGroup,struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,CountType>*Dictionary<CountryGroup,CountType>*Dictionary<SexGroup,CountType>)>()
let allInterestsGroups =
    Dictionary<InterestsGroup,struct(Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,CountType>*Dictionary<CountryGroup,CountType>*Dictionary<SexGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allCountrySexGroups =
    Dictionary<struct(CountryGroup*SexGroup),struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allCitySexGroups =
    Dictionary<struct(CityGroup*SexGroup),struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,CountType>)>()
let allCountryStatusGroups =
    Dictionary<struct(CountryGroup*StatusGroup),struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,CountType>)>()
let allCityStatusGroups =
    Dictionary<struct(CityGroup*StatusGroup),struct(Dictionary<InterestsGroup,CountType>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,CountType>)>()