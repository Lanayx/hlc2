module HCup.Dictionaries

open System.Collections.Generic
open HCup.Models
open System

type FnameGroup = byte
type SnameGroup = int16
type BirthGroup = int16
type JoinedGroup = int16
type SexGroup = byte
type StatusGroup = byte
type CityGroup = int16
type CountryGroup = byte
type InterestsGroup = byte
type CountType = int
type FourthField = (struct(CountType*Dictionary<JoinedGroup,CountType>*Dictionary<BirthGroup,CountType>))


let fnamesWeightDictionary = Dictionary<string, byte>()
let snamesWeightDictionary = Dictionary<string, int16>()
let mutable snamesWeightDictionaryReverse = Dictionary<int16, string>()
let citiesWeightDictionary = Dictionary<string, int16>()
let countriesWeightDictionary = Dictionary<string, byte>()
let interestsWeightDictionary = Dictionary<string, byte>()

let mutable fnamesSerializeDictionary : byte[][] = null
let mutable snamesSerializeDictionary : byte[][] = null
let mutable citiesSerializeDictionary : byte[][] = null
let mutable countriesSerializeDictionary : byte[][] = null
let mutable interestsSerializeDictionary : byte[][] = null

// key is likeId,  value is dict of key=userId, value=sumOfTs*tsCount
let likesIndex: SortedList<int, struct(single*byte)>[] = Array.zeroCreate(1400000)
// key is cityWeight, value is usersId's set
let citiesIndex = Dictionary<CityGroup, SortedSet<int>>()
let countriesIndex = Dictionary<CountryGroup, SortedSet<int>>()
let fnamesIndex = Dictionary<FnameGroup, SortedSet<int>>()
let emailsDictionary = HashSet<string>()

// GROUPING

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

// RECOMMEND

let mutable bestMaleUsers = ResizeArray<Account>()
let mutable bestMaleUsers2 = ResizeArray<Account>()
let mutable bestMaleUsers3 = ResizeArray<Account>()
let mutable bestSimpleMaleUsers = ResizeArray<Account>()
let mutable bestSimpleMaleUsers2 = ResizeArray<Account>()
let mutable bestSimpleMaleUsers3 = ResizeArray<Account>()
let mutable bestFemaleUsers = ResizeArray<Account>()
let mutable bestFemaleUsers2 = ResizeArray<Account>()
let mutable bestFemaleUsers3 = ResizeArray<Account>()
let mutable bestSimpleFemaleUsers = ResizeArray<Account>()
let mutable bestSimpleFemaleUsers2 = ResizeArray<Account>()
let mutable bestSimpleFemaleUsers3 = ResizeArray<Account>()

let getRecommendUsers sex =
    if sex = Common.female
    then
        seq {
            yield! bestFemaleUsers
            yield! bestFemaleUsers2
            yield! bestFemaleUsers3
            yield! bestSimpleFemaleUsers
            yield! bestSimpleFemaleUsers2
            yield! bestSimpleFemaleUsers3
        }
    else
        seq {
            yield! bestMaleUsers
            yield! bestMaleUsers2
            yield! bestMaleUsers3
            yield! bestSimpleMaleUsers
            yield! bestSimpleMaleUsers2
            yield! bestSimpleMaleUsers3
        }
