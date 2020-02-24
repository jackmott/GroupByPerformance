# `GroupBy` Performance
Looking at the performance of `GroupBy` in various scenarios

This project is all .NET Core 3.1, previous editions may differ due to newer
.NET Core LINQ optimizations.

For each use case I will test two scenarios, each using a toy `Bill` class standing in for a typical thing one might group on.
Scenario 1 will have ~10,000 keys each with ~10 bills, scenario 2 will have ~10 keys each with ~10,000 bills.


## Aggregating by group
One thing you might use group by for is to compute some aggregate value by grouping. For instance with our bill, we might want to know the sum of the totals for each bill type. `Group By` provides a very concise way to do this:

```c#
return bills.GroupBy(b => b.BillType).Select(grouping => grouping.Sum(b => b.Total)).ToList();
```
This will return a List of the sums of all the totals or each group type.

Avoiding group by, another way we could compute the sums is like so:
```c#
 var sums = new Dictionary<int, double>();
 foreach (var bill in bills)
 {
     double total;
     if (!sums.TryGetValue(bill.BillType, out total))
     {
         sums.Add(bill.BillType, bill.Total);
     }
     else
     {                    
         sums[bill.BillType] = total + bill.Total;
     }
 }
 return sums.Values.ToList();
```
This is a lot more code, and we might expect this to perform worse, since we are creating a `Dictionary` to hold a bunch of intermediate data before we aggregate. But it turns out, so is `GroupBy`, let's look at a BenchmarkDotNet comparison:

|             Method | GroupCount |      Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 |  Allocated |
|------------------- |----------- |----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
|       GroupBy_Sums |         10 |  3.323 ms | 0.0200 ms | 0.0187 ms | 367.1875 | 292.9688 | 210.9375 | 2566.69 KB |
|    Dictionary_Sums |         10 |  1.553 ms | 0.0054 ms | 0.0048 ms |        - |        - |        - |    1.16 KB |
|       GroupBy_Sums |      10000 | 12.396 ms | 0.1093 ms | 0.1022 ms | 578.1250 | 296.8750 |  78.1250 | 4586.58 KB |
|    Dictionary_Sums |      10000 |  2.181 ms | 0.0178 ms | 0.0158 ms | 121.0938 | 101.5625 |  85.9375 |  998.24 KB |

Doing the aggregation as you go, if possible, saves a ton of time and GC pressure. This is because `GroupBy` can't perform this operation in a totally lazy manner, it is having to create a lookup table behind the scenes, one that is filled with a collection of Bills, rather than just the current sum.

## Building up a cache for later use

Another scenario we often use `GroupBy` for is to create caches/lookup tables for use later in our application.  For instance a website may pull down data from a database on startup, and create an in memory cache to avoid hammering the database for frequently queries but rarely changed data. With this use case, there are two concerns. How long it takes to create the cache, and then how well the cache performs when using it. We will take a look at both. In our case we want some kind of lookup table that lets us get a collection of `Bills` when we provide the `BillType`. We could do that by using `GroupBy`, `ToLookup`, or by creating a `Dictionary` by hand:
``` c#
 
public Dictionary<int, IEnumerable<Bill>> GroupBy_Cache()
{
    return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key, g => (IEnumerable<Bill>)g);
}
 
public ILookup<int,Bill> Lookup_Cache()
{
    return bills.ToLookup(b => b.BillType);
}

public Dictionary<int,List<Bill>> Dictionary_Cache()
{
    var dict = new Dictionary<int, List<Bill>>();
    foreach (var bill in bills)
    {
        List<Bill> billList;
        if (!dict.TryGetValue(bill.BillType, out billList))
        {
            billList = new List<Bill>();
            dict.Add(bill.BillType, billList);
        }
        billList.Add(bill);

    }
    return dict;
}
```

Here is how long these approaches take to build:


|           Method | GroupCount |      Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|----------------- |----------- |----------:|----------:|----------:|---------:|---------:|---------:|----------:|
|    GroupBy_Cache |         10 |  2.582 ms | 0.0093 ms | 0.0087 ms | 343.7500 | 265.6250 | 187.5000 |   2.51 MB |
|     Lookup_Cache |         10 |  2.507 ms | 0.0107 ms | 0.0089 ms | 343.7500 | 265.6250 | 187.5000 |    2.5 MB |
| Dictionary_Cache |         10 |  2.173 ms | 0.0092 ms | 0.0086 ms | 312.5000 | 226.5625 | 156.2500 |    2.5 MB |
|    GroupBy_Cache |      10000 | 12.101 ms | 0.2383 ms | 0.3340 ms | 609.3750 | 328.1250 | 203.1250 |   4.75 MB |
|     Lookup_Cache |      10000 | 11.864 ms | 0.0617 ms | 0.0577 ms | 515.6250 | 218.7500 |  62.5000 |   3.85 MB |
| Dictionary_Cache |      10000 |  8.708 ms | 0.1029 ms | 0.0962 ms | 500.0000 | 312.5000 | 140.6250 |   3.58 MB |

It is worth noting that the `Dictionary` case could be optimized further in cases when you know, or approximately know, how many groupings will be in the data ahead of time, by setting the initial `Dictionary` capacity.

## Using the cache

The above startup times may all be roughly equivalent for many use cases, especially since they will usually be one time, or rare operations. But what about using the cache, which will be done repeatedly, and where performance is more likely to be important?  Here is a test of each cache that just grabs a single BillType from each cache, and iterates over all the bills in that group to sum them up:

```c#
 [Benchmark]
 public double GroupByCache_Use()
 {          
     var bills = groupByCache[5];
     double total = 0.0;
     foreach (var bill in bills)
     {
         total += bill.Total;
     }
     return total;
 }

 [Benchmark]
 public double LookupCache_Use()
 {                        
     var bills = lookupCache[5];
     double total = 0.0;
     foreach (var bill in bills)
     {
         total += bill.Total;
     }                    
     return total;
 }

 [Benchmark]
 public double DictionaryCache_Use()
 {
     var bills = dictionaryCache[5];
     double total = 0.0;
     for(int j = 0; j < bills.Count;j++)
     {
         total += bills[j].Total;
     }
     return total;                    
 }
```

With the following results:

|              Method | GroupCount |         Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------- |----------- |-------------:|----------:|----------:|-------:|------:|------:|----------:|
|    GroupByCache_Use |         10 | 58,547.40 ns | 46.101 ns | 38.496 ns |      - |     - |     - |      40 B |
|     LookupCache_Use |         10 | 58,321.58 ns | 58.868 ns | 52.185 ns |      - |     - |     - |      41 B |
| DictionaryCache_Use |         10 | 11,767.20 ns | 32.860 ns | 29.130 ns |      - |     - |     - |         - |
|    GroupByCache_Use |      10000 |     75.96 ns |  0.375 ns |  0.332 ns | 0.0048 |     - |     - |      40 B |
|     LookupCache_Use |      10000 |     70.00 ns |  1.129 ns |  1.056 ns | 0.0048 |     - |     - |      40 B |
| DictionaryCache_Use |      10000 |     14.73 ns |  0.068 ns |  0.064 ns |      - |     - |     - |         - |

The differences here are due almost entirely to being able to leverage that the `Dictionary` cache has a `List` inside of it, allowing us to avoid the overhead and allocations used when using an `IEnumerable`. If you were to use `.Sum()` in all cases for instance, instead of a foreach or for loop to compute the sums, all 3 caches would perform very nearly the same.  Also it should be noted the time to retrieve a group from the cache is very nearly the same in all 3 cases. Only the time and allocations to iterate over the groups differ.

## Using GroupBy to produce Lists for each group

If you want to create a cache that is as efficient as possible when being used, you can still use GroupBy, like so:

```c#
public Dictionary<int, List<Bill>> GroupByList_Cache()
{
    return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key,g => g.ToList());
}
```

Unforunately while this cache will be fast to use, it takes much longer and way more allocations to create:

|            Method | GroupCount |      Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|------------------ |----------- |----------:|----------:|----------:|---------:|---------:|---------:|----------:|
| GroupByList_Cache |         10 |  3.142 ms | 0.0266 ms | 0.0249 ms | 460.9375 | 359.3750 | 210.9375 |   3.27 MB |
|  Dictionary_Cache |         10 |  2.088 ms | 0.0091 ms | 0.0081 ms | 328.1250 | 250.0000 | 171.8750 |    2.5 MB |
| GroupByList_Cache |      10000 | 14.683 ms | 0.2589 ms | 0.2422 ms | 781.2500 | 468.7500 | 187.5000 |   6.04 MB |
|  Dictionary_Cache |      10000 |  8.577 ms | 0.1040 ms | 0.0973 ms | 468.7500 | 281.2500 | 140.6250 |   3.58 MB |


## A real life example from work (and a large open source project)

At work we had some code running in a web framework on every single request that was a bit slow, GroupBy was being used to extract just the first element out of each group into a `Dictionary`. Here is an approximation of the original code and the optimized version:

```c#
 [Benchmark]
 public Dictionary<int, Bill> GroupbyFirst()
 {
     return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key, g => g.First());
 }
 [Benchmark]
 public Dictionary<int, Bill> DictionaryFirst()
 {
     var dict = new Dictionary<int, Bill>();
     foreach (var bill in bills)
     {
         if (!dict.ContainsKey(bill.BillType))
         {
             dict.Add(bill.BillType, bill);
         }
     }
     return dict;
 }
```

And the results:
|          Method | GroupCount |      Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 |  Allocated |
|---------------- |----------- |----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
|    GroupbyFirst |         10 |  2.481 ms | 0.0068 ms | 0.0064 ms | 367.1875 | 285.1563 | 210.9375 | 2565.98 KB |
| DictionaryFirst |         10 |  1.140 ms | 0.0023 ms | 0.0019 ms |        - |        - |        - |    1.01 KB |
|    GroupbyFirst |      10000 | 11.337 ms | 0.0967 ms | 0.0858 ms | 625.0000 | 343.7500 | 140.6250 | 4859.43 KB |
| DictionaryFirst |      10000 |  1.539 ms | 0.0056 ms | 0.0046 ms | 101.5625 |  83.9844 |  76.1719 |  920.06 KB |


## Have your cake and eat it to?

The brevity that GroupBy affords is really nice.  In many cases you can get that same brevity with better performance by writing your own generic extension methods, to turn Enumerables into grouped dictionaries:

```c#
    public static Dictionary<K, List<V>> GroupByDictionary<K, V>(this IEnumerable<V> items, Func<V, K> keySelector)
    {
        var dictionary = new Dictionary<K, List<V>>();
        foreach (var item in items)
        {
            List<V> grouping;
            var key = keySelector(item);
            if (!dictionary.TryGetValue(key, out grouping))
            {
                grouping = new List<V>(1);
                dictionary.Add(key, grouping);
            }
            grouping.Add(item);
        }
        return dictionary;
    }

    public static Dictionary<K, List<V>> GroupByDictionary<U,K, V>(this IEnumerable<U> items, Func<U, K> keySelector, Func<U,V> valueSelector)
    {
        var dictionary = new Dictionary<K, List<V>>();
        foreach (var item in items)
        {
            List<V> grouping;
            var key = keySelector(item);
            if (!dictionary.TryGetValue(key, out grouping))
            {
                grouping = new List<V>(1);
                dictionary.Add(key, grouping);
            }
            grouping.Add(valueSelector(item));
        }
        return dictionary;
    }
}
```
