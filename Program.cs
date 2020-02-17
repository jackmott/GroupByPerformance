using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace GroupBy
{
    /**
    * A toy class representing the sort of thing we tend to groupby on
    */
    public class Bill    
    {
        public int BillType;
        public double Total;

        public Bill(int type, double total)
        {
            BillType = type;
            Total = total;
        }
    }

    [MemoryDiagnoser]   
    public class Benchmarks
    {
       
        [Params(10, 10000)]
        public int GroupCount { get; set; }

        const int SIZE = 100000;

        IEnumerable<Bill> bills;

       
        Dictionary<int, IEnumerable<Bill>> groupByCache;
        ILookup<int, Bill> lookupCache;
        Dictionary<int, List<Bill>> dictionaryCache;

        public Benchmarks()
        {            
        }

        /*
        * This setup step is not measured, and creates data for our benchmarks to work with
        */
        [GlobalSetup]
        public void GlobalSetup()
        {
            Random r = new Random(4);

            List<Bill> bills = new List<Bill>();
            bills.Add(new Bill(5, 100));
            for (int i = 0; i < SIZE; i++)
            {
                var group = r.Next(0, GroupCount);
                var total = r.Next(-100, 101);
                bills.Add(new Bill(group, total));
            }
            this.bills = bills;
            groupByCache = GroupBy_Cache();
            lookupCache = Lookup_Cache();
            dictionaryCache = Dictionary_Cache();

        }

        /**
        * One thing you might use groupby for is to aggregate data by each group. In the following
        * examples we measure 3 ways to get the sum totals by group type
        */


     //   [Benchmark]
        public List<double> GroupBy_Sums() {
            return bills.GroupBy(b => b.BillType).Select(grouping => grouping.Sum(b => b.Total)).ToList();
        }   

     //   [Benchmark]
        public List<double> Dictionary_SumsSpecialized() {
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
        }

     //   [Benchmark]
        public Dictionary<int, IEnumerable<Bill>> GroupBy_Cache()
        {
            return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key, g => (IEnumerable<Bill>)g);
        }
      //  [Benchmark]
        public Dictionary<int, List<Bill>> GroupByList_Cache()
        {
            return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key,g => g.ToList());
        }

      //  [Benchmark]
        public ILookup<int,Bill> Lookup_Cache()
        {
            return bills.ToLookup(b => b.BillType);
        }

       // [Benchmark]
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



      //  [Benchmark]
        public double GroupByCache_Use()
        {
            return groupByCache[5].Sum(b => b.Total);
            var bills = groupByCache[5];
            /*double total = 0.0;
            /*foreach (var bill in bills)
            {
                total += bill.Total;
            }
            return total*/
        }

     //   [Benchmark]
        public double LookupCache_Use()
        {
            return lookupCache[5].Sum(b => b.Total);
            /*
            var bills = lookupCache[5];
            double total = 0.0;
            foreach (var bill in bills)
            {
                total += bill.Total;
            }                    
            return total;
            */
        }

      //  [Benchmark]
        public double DictionaryCache_Use()
        {
            return dictionaryCache[5].Sum(b => b.Total);
            /*
            var bills = dictionaryCache[5];
            double total = 0.0;
            for(int j = 0; j < bills.Count;j++)
            {
                total += bills[j].Total;
            }
            return total;                    
            */
        }

    //    [Benchmark]
        public Bill GroupByCache_Get()
        {
            return groupByCache[5].First();
        }

     //   [Benchmark]
        public Bill LookupCache_Get()
        {
            return lookupCache[5].First();
        }

     //   [Benchmark]
        public Bill DictionaryCache_Get()
        {
            return dictionaryCache[5].First();
        }
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

    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Benchmarks>();
        }
    }
}
