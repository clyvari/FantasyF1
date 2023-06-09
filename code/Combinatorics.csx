#nullable enable

public static class Combinatorics
{
    public static List<ulong> Combine(List<uint> al, List<uint> bl)
    {
        var res = new List<ulong>(al.Count*bl.Count);
        foreach(var a in al)
        {
            foreach(var b in bl)
            {
                res.Add(((ulong)a) << 32 | b);
            }
        }
        return res;
    }

    /// <summary>
    /// Only works up to 32 possible items
    /// </summary>
    ///
    public static List<uint> GenerateCombinations(int possibleItems, int drawnItems)
    {
        var res = new List<uint>();
        var config = Enumerable.Range(0, drawnItems).Reverse().ToArray();
        var done = false;
        while (!done)
        {
            uint data = 0;
            int incrementIndex = -1;
            var hasIncremented = false;
            for(var i = 0; i < config.Length; i++)
            {
                data |= (uint)1 << config[i];
                if(!hasIncremented)
                {
                    if(config[i] < (possibleItems-i-1))
                    {
                        config[i]++;
                        hasIncremented = true;
                        incrementIndex = i;
                    }
                }
            }
            for(var i = 0; i < incrementIndex; i++)
            {
                config[i] = config[incrementIndex]+incrementIndex-i;
            }
            res.Add(data);
            if(!hasIncremented) done = true;
        }
        return res;
    }
}