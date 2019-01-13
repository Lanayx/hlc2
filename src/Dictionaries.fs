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

type BirthGroup = int16
type JoinedGroup = int16
type SexGroup = byte
type StatusGroup = byte
type CityGroup = int64
type CountryGroup = int64
type InterestsGroup = int64
type CountType = int
type FourthField = (struct(CountType*Dictionary<JoinedGroup,CountType>*Dictionary<BirthGroup,CountType>))

let allCountryGroups =
    Dictionary<CountryGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allCityGroups =
    Dictionary<CityGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allSexGroups =
    Dictionary<SexGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allStatusGroups =
    Dictionary<StatusGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<SexGroup,FourthField>)>()
let allInterestsGroups =
    Dictionary<InterestsGroup,struct(Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allCountrySexGroups =
    Dictionary<struct(CountryGroup*SexGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,FourthField>)>()
let allCitySexGroups =
    Dictionary<struct(CityGroup*SexGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,FourthField>)>()
let allCountryStatusGroups =
    Dictionary<struct(CountryGroup*StatusGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>)>()
let allCityStatusGroups =
    Dictionary<struct(CityGroup*StatusGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>)>()