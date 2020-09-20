using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class chaos : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] tiles;
    public Renderer[] tileRenders;
    public TextMesh[] tileTexts;
    public Texture[] patterns;
    public Color[] colors;
    public Color black;
    public Color orange;
    public Color beige;
    public Font shapeFont;
    public Font letterFont;
    public Font[] boozleFonts;
    public Material shapeMat;
    public Material letterMat;
    public Material[] boozleMats;

    private int[] values = new int[25];
    private List<int> solution = new List<int>();
    private List<CL>[] complexityLevels = new List<CL>[25];
    private int stage;
    private int enteringStage;
    private bool[] incrementNextStage = new bool[25];
    private bool lastStage;

    private Coroutine[] boozleglyphCoroutines = new Coroutine[25];
    private Coroutine[] morseCodeCoroutines = new Coroutine[25];

    private static readonly string[] colorNames = new string[7] { "red", "green", "blue", "cyan", "magenta", "yellow", "white" };
    private static readonly string[] directionNames = new string[4] { "up", "right", "down", "left" };
    private static readonly string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string[] morseLetters = new string[36] { "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----.", ".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..", ".---", "-.-", ".-..", "--", "-.", "---", ".--.", "--.-", ".-.", "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--.." };

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;
    private bool activated;
    private bool cantPress;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        module.OnActivate += delegate () { activated = true; audio.PlaySoundAtTransform("activate", transform); };
        foreach (KMSelectable tile in tiles)
            tile.OnInteract += delegate () { PressTile(tile); return false; };
    }

    void Start()
    {
        var startingSquare = rnd.Range(0, 25);
        for (int i = 0; i < 25; i++)
        {
            complexityLevels[i] = i == startingSquare ? new List<CL> { CL.primary } : new List<CL> { CL.inactive };
            tileTexts[i].text = "";
        }
        GenerateStage();
    }

    void PressTile(KMSelectable tile)
    {
        tile.AddInteractionPunch(.5f);
        if (moduleSolved || !activated || cantPress)
            return;
        var ix = Array.IndexOf(tiles, tile);
        if (ix == solution[enteringStage])
        {
            Debug.LogFormat("[Chaos #{0}] You pressed {1}. That was correct.", moduleId, Coordinate(ix));
            BlankButton(ix);
            if (!(complexityLevels[ix].Contains(CL.pattern) && complexityLevels[ix].Contains(CL.boozleglyph) && complexityLevels[ix].Contains(CL.morse)))
                incrementNextStage[ix] = true;
            audio.PlaySoundAtTransform("press", tile.transform);
            enteringStage++;

            if (enteringStage == solution.Count())
            {
                if (!lastStage)
                {
                    for (int i = 0; i < 25; i++)
                        BlankButton(i);
                    stage++;
                    StartCoroutine(WaitForNewStage());
                }
                else
                {
                    // To-do: Solve + animation
                }
            }
        }
        else
        {
            Debug.LogFormat("[Chaos #{0}] You pressed {1}. That was incorrect. Strike!", moduleId, Coordinate(ix));
            audio.PlaySoundAtTransform("strike", tile.transform);
            module.HandleStrike();
        }
    }

    void GenerateStage()
    {
        // To-do: Check if this stage is the last

        // Iincrement levels of complexity
        Debug.Log(incrementNextStage.Join());
        for (int i = 0; i < 25; i++)
        {
            if (incrementNextStage[i])
            {
                var level = complexityLevels[i];
                if (level.Contains(CL.inactive))
                {
                    level.Remove(CL.inactive);
                    level.Add(CL.primary);
                }
                else
                {
                    var possibleIncrements = new List<CL>();
                    if (level.Contains(CL.primary))
                    {
                        possibleIncrements.Add(CL.secondary);
                        possibleIncrements.Add(CL.shape);
                    }
                    else if (level.Contains(CL.secondary))
                    {
                        possibleIncrements.Add(CL.shape);
                        possibleIncrements.Add(CL.pattern);
                    }
                    if (level.Contains(CL.shape))
                    {
                        possibleIncrements.Add(CL.letter);
                        possibleIncrements.Add(CL.morse);
                    }
                    else if (level.Contains(CL.letter))
                    {
                        possibleIncrements.Add(CL.boozleglyph);
                        possibleIncrements.Add(CL.morse);
                    }
                    level.Add(possibleIncrements.Where(x => !level.Contains(x)).PickRandom());
                }

                level = complexityLevels[i];
                if (level.Contains(CL.secondary))
                    level.Remove(CL.primary);
                if (level.Contains(CL.pattern))
                    level.Remove(CL.secondary);
                if (level.Contains(CL.letter))
                    level.Remove(CL.shape);
                if (level.Contains(CL.boozleglyph))
                    level.Remove(CL.letter);
            }

            incrementNextStage[i] = false;
            values[i] = 0;
        }

        enteringStage = 0;
        Debug.LogFormat("[Chaos {0}] STAGE {1}:", moduleId, stage + 1);

        // Calculate A values before % 15
        tryAgain:
        var squareColors = new string[25].Select(x => x = "").ToArray();
        var solutionBools = new bool[25];
        var boozleCoroutines = new List<BoozleCoroutineInfo>();
        var morseCoroutines = new List<MorseCoroutineInfo>();
        for (int i = 0; i < 25; i++)
        {
            var A = 0;
            if (complexityLevels[i].Contains(CL.inactive))
                tileRenders[i].material.SetColor("_ColorA", beige);
            if (complexityLevels[i].Contains(CL.primary))
            {
                var color = rnd.Range(0, 3);
                tileRenders[i].material.SetColor("_ColorA", colors[color]);
                squareColors[i] += "RGB"[color];
                Debug.LogFormat("[Chaos #{0}] {1} is {2}.", moduleId, Coordinate(i), colorNames[color]);
                A += color; // TEMPORARY
            }
            if (complexityLevels[i].Contains(CL.secondary))
            {
                var color = rnd.Range(0, 4);
                tileRenders[i].material.SetColor("_ColorA", colors[color + 3]);
                squareColors[i] += "CMYW"[color];
                Debug.LogFormat("[Chaos #{0}] {1} is {2}.", moduleId, Coordinate(i), colorNames[color + 3]);
                A += color; // TEMPORARY
            }
            if (complexityLevels[i].Contains(CL.pattern))
            {
                var blackColor = rnd.Range(0, 7);
                var whiteColor = rnd.Range(0, 7);
                while (whiteColor == blackColor)
                    whiteColor = rnd.Range(0, 7);
                var pattern = rnd.Range(0, 5);
                tileRenders[i].material.SetColor("_ColorA", colors[blackColor]);
                tileRenders[i].material.SetColor("_ColorB", colors[whiteColor]);
                tileRenders[i].material.SetTexture("_PatternTex", patterns[pattern]);
                squareColors[i] += "RGBCMYW"[blackColor];
                squareColors[i] += "RGBCMYW"[whiteColor];
                A += blackColor; // TEMPORARY
            }
            if (complexityLevels[i].Contains(CL.shape))
            {
                var shape = rnd.Range(0, 4);
                tileTexts[i].font = shapeFont;
                tileTexts[i].GetComponent<Renderer>().material = shapeMat;
                tileTexts[i].text = "ABCD"[shape].ToString();
                tileTexts[i].transform.localScale = new Vector3(.025f, .025f, .025f);
                A += shape; // TEMPORARY
            }
            if (complexityLevels[i].Contains(CL.letter))
            {
                var letter = rnd.Range(1, 27);
                tileTexts[i].font = letterFont;
                tileTexts[i].GetComponent<Renderer>().material = letterMat;
                tileTexts[i].text = base36[letter + 9].ToString();
                var s = tileTexts[i].text == "Q" ? .025f : .035f;
                tileTexts[i].transform.localScale = new Vector3(s, s, s);
                A += letter; // TEMPORARY
            }
            if (complexityLevels[i].Contains(CL.boozleglyph))
            {
                var set = rnd.Range(0, 6);
                tileTexts[i].font = boozleFonts[set];
                tileTexts[i].GetComponent<Renderer>().material = boozleMats[set];
                tileTexts[i].transform.localScale = new Vector3(.035f, .035f, .035f);
                boozleglyphsTryAgain:
                var letters = Enumerable.Range(0, 26).ToList().Shuffle().Take(5).ToArray();
                var check = new int[3]; // Used to determine if the 5 boozleglyphs chosen are enough to uniquely determine a set
                var rows = new string[3] { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
                for (int j = 0; j < 5; j++)
                    check[Array.IndexOf(rows, rows.First(x => x.Contains(alphabet[letters[j]])))]++;
                if ((check[0] == 0 && check[1] == 0) || (check[1] == 0 && check[2] == 0) || (check[0] == 0 && check[2] == 0))
                    goto boozleglyphsTryAgain;
                A += set; // TEMPORARY
                boozleCoroutines.Add(new BoozleCoroutineInfo(i, letters));
            }
            if (complexityLevels[i].Contains(CL.morse))
            {
                var letter = rnd.Range(0, 36);
                morseCoroutines.Add(new MorseCoroutineInfo(i, letter, squareColors[i]));
                A += letter; // TEMPORARY
            };
            values[i] = A;

            // Use colors and positions to convert A to binary and determine which tiles need to be pressed
            if (!complexityLevels[i].Contains(CL.inactive))
            {
                var colorDirections = new string[4] { "RC", "BY", "W", "GM" };
                var offsets = new int[4] { -5, 1, 5, -1 };
                var binary = Convert.ToString(values[i] % 15, 2).PadLeft(4, '0');
                Debug.LogFormat("[Chaos #{0}] The final value of A is {1}, which modulo 15 and converted to binary is {2}.", moduleId, values[i], binary);
                var direction = Array.IndexOf(colorDirections, colorDirections.First(x => x.Contains(squareColors[i][0])));
                Debug.LogFormat("[Chaos #{0}] The starting direction is {1}.", moduleId, directionNames[direction]);
                for (int j = 0; j < 4; j++)
                {
                    if (binary[j] == '1' && !((direction % 2 == 0 ? i / 5 : i % 5) == (direction == 0 || direction == 3 ? 0 : 4)))
                        solutionBools[i + offsets[direction]] = !solutionBools[i + offsets[direction]];
                    direction = (direction + 1) % 4;
                }
            }
        }

        // Sort the tiles in the solution
        if (!solutionBools.Any(x => x))
            goto tryAgain;
        solution = solutionBools.Select((b, i) => new { index = i, value = b }).Where(o => o.value).Select(o => o.index).OrderBy(x => values[x]).ThenBy(x => x).ToList();
        Debug.LogFormat("[Chaos #{0}] SOLUTION: {1}", moduleId, solution.Select(x => Coordinate(x)).Join(", "));
        foreach (BoozleCoroutineInfo c in boozleCoroutines) // This is done with boozleglyph and Morse coroutines to prevent overlapping coroutines in the case of the module needing to try again
            boozleglyphCoroutines[c.index] = StartCoroutine(BoozleCycle(c));
        foreach (MorseCoroutineInfo c in morseCoroutines)
            morseCodeCoroutines[c.index] = StartCoroutine(FlashMorse(c));
        cantPress = false;
    }

    IEnumerator BoozleCycle(BoozleCoroutineInfo c)
    {
        resetCycle:
        for (int i = 0; i < 5; i++)
        {
            tileTexts[c.index].text = alphabet[c.letters[i]].ToString();
            yield return new WaitForSeconds(1f);
        }
        goto resetCycle;
    }

    IEnumerator FlashMorse(MorseCoroutineInfo c)
    {
        var morseLetter = morseLetters[c.letter];
        var colorLetters = "RGBCMYW";
        resetCycle:
        for (int j = 0; j < morseLetter.Length; j++)
        {
            tileRenders[c.index].material.SetColor("_ColorA", orange);
            tileRenders[c.index].material.SetColor("_ColorB", orange);
            yield return new WaitForSeconds(morseLetter[j] == '.' ? .25f : .75f);
            tileRenders[c.index].material.SetColor("_ColorA", colors[colorLetters.IndexOf(c.baseColors[0])]);
            if (c.baseColors.Length == 2)
                tileRenders[c.index].material.SetColor("_ColorB", colors[colorLetters.IndexOf(c.baseColors[1])]);
            yield return new WaitForSeconds(.5f);
        }
        yield return new WaitForSeconds(1f);
        goto resetCycle;
    }

    void BlankButton(int ix)
    {
        if (boozleglyphCoroutines[ix] != null)
        {
            StopCoroutine(boozleglyphCoroutines[ix]);
            boozleglyphCoroutines[ix] = null;
        }
        if (morseCodeCoroutines[ix] != null)
        {
            StopCoroutine(morseCodeCoroutines[ix]);
            morseCodeCoroutines[ix] = null;
        }
        tileRenders[ix].material.SetColor("_ColorA", black);
        tileRenders[ix].material.SetColor("_ColorB", black);
        tileTexts[ix].text = "";
    }

    IEnumerator WaitForNewStage()
    {
        cantPress = true;
        yield return new WaitForSeconds(4f);
        audio.PlaySoundAtTransform("stage", transform);
        GenerateStage();
    }

    string Coordinate(int x)
    {
        var s1 = "ABCDE"[x % 5].ToString();
        var s2 = ((x / 5) + 1).ToString();
        return s1 + s2;
    }


    // Twitch Plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} ";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string input)
    {
        yield return null;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
    }
}
