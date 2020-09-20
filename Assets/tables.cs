public enum CL
{
    inactive,
    primary,
    secondary,
    pattern,
    shape,
    letter,
    boozleglyph,
    morse
}

class BoozleCoroutineInfo
{
    public int index;
    public int[] letters;

    public BoozleCoroutineInfo(int i, int[] l)
    {
        index = i;
        letters = l;
    }
}

class MorseCoroutineInfo
{
    public int index;
    public int letter;
    public string baseColors;

    public MorseCoroutineInfo(int i, int l, string s)
    {
        index = i;
        letter = l;
        baseColors = s;
    }
}
