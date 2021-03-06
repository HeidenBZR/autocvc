﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace VAtlas
{

    public partial class Atlas
    {
        #region Variables
        public string Version;
        public string[] Vowels;
        public string[] Consonants;
        public string[] Rests;
        public List<string> AliasTypes;
        public Dictionary<string, string> Format;
        public Dictionary<string, string> FormatRegex;
        public List<string[]> AliasReplaces;
        public List<string[]> PhonemeReplaces;
        public Dict Dict;
        public RuleManager RuleManager;

        public bool KeepWordsEndings = false;
        public bool KeepWordsBeginnigs = false;
        public bool KeepCC = false;
        public bool KeepCV = true;

        public string Melisma = "&m;";


        public string ExampleWord;

        public bool IsLoaded = false;
        public bool HasDict { get { return Dict.IsEnabled; } }
        public bool IsDefault = false;
        public string VoicebankType;

        private string VowelPattern;
        private string ConsonantPattern;
        private string RestPattern;

        public static string DefaultVoicebankType => "CVC RUS";

        public static Atlas Current;

        string _dir;

        #endregion

        public Atlas(string dir)
        {
            _dir = dir;
            Current = this;
            RuleManager = new RuleManager(this);
            Load();
        }

        public void Reload()
        {
            Load();
        }

        public bool IsRest(string phoneme) { return Rests.Contains(phoneme) || phoneme == "R" || phoneme.Trim(' ') == ""; }
        public bool IsConsonant(string phoneme) { return Consonants.Contains(phoneme); }
        public bool IsVowel(string phoneme)
        {
            return Vowels.Contains(phoneme);
        }

        public bool IsAtlasPhoneme(string phoneme)
        {
            return Rests.Contains(phoneme) || Consonants.Contains(phoneme) || Vowels.Contains(phoneme); 

        }

        public string GetDictPath()
        {
            return PathResolver.GetResourceFile(Path.Combine(@"Atlas", VoicebankType + ".dict"));
        }

        public string GetAtlasPath()
        {
            return PathResolver.GetResourceFile(Path.Combine(@"Atlas", VoicebankType + ".Atlas"));
        }

        public static string GetAtlasPath(string voicebankType)
        {
            return PathResolver.GetResourceFile(Path.Combine(@"Atlas", voicebankType + ".Atlas"));
        }

        public string GetAliasType(string alias)
        {
            if (alias == NumberManager.DELETE) return "";
            if (IsRest(alias)) return "R";
            if (IsVowel(alias)) return "V";
            if (IsConsonant(alias)) return "C";
            foreach (string alias_type in AliasTypes)
            {
                string pattern = FormatRegex[alias_type];
                if (Regex.IsMatch(alias, pattern))
                {
                    var value = Regex.Match(alias, pattern).Value;
                        if (value == alias)
                            return alias_type;
                }
            }
            return "";
            //throw new Exception("Это говно какое-то а не алиас ясно?");
        }

        public string[] GetPhonemes(string alias)
        {
            if (IsRest(alias)) return new string[] {};
            if (IsVowel(alias) || IsConsonant(alias)) return new string[] { alias };
            foreach (string alias_type in AliasTypes)
            {
                string pattern = FormatRegex[alias_type];
                if (Regex.IsMatch(alias, pattern))
                {
                    string value = Regex.Match(alias, pattern).Value;
                    if (value == alias)
                    {
                        List<string> st = Regex.Split(alias, pattern).ToList();
                        st.Remove(st.First());
                        st.Remove(st.Last());
                        return st.ToArray();
                    }
                }
            }
            throw new Exception($"Can't extract phonemes from [{alias}]");
        }

        public string[] GetPhonemes(string alias, string alias_type)
        {
            if (IsRest(alias)) return new string[] { };
            string pattern = FormatRegex[alias_type];
            if (Regex.IsMatch(alias, pattern))
            {
                string attempt = Regex.Match(alias, pattern).Value;
                if (attempt == alias)
                {
                    List<string> st = Regex.Split(alias, pattern).ToList();
                    st.Remove(st.First());
                    st.Remove(st.Last());
                    return st.ToArray();
                }
            }
            throw new Exception($"Can't extract phonemes from {alias}");
        }

        public bool MatchPhonemeType(string PhonemeType, string phoneme)
        {
            switch (PhonemeType)
            {
                case "%C%":
                    if (Consonants.Contains(phoneme)) return true;
                    else return false;
                case "%V%":
                    if (Vowels.Contains(phoneme)) return true;
                    else return false;
                default:
                    throw new Exception($"Unknown phoneme: {phoneme}");
            }
        }

        public string GetAlias(string alias_type, string[] phonemes)
        {
            if (alias_type == "R") return " ";
            string ph = "%.%";
            if (!Format.ContainsKey(alias_type))
                throw new Exception($"Cannot found alias type format for alias type: {alias_type} for {Singer.Current.VoicebankType}");
            string format = Format[alias_type];
            int i = 0;
            while (Regex.IsMatch(format, ph))
            {
                if (i >= phonemes.Length)
                    throw new Exception($"Not enough phonemes to format alias {alias_type} for {Singer.Current.VoicebankType}");
                string ph_type = Regex.Match(format, ph).Value;
                if (MatchPhonemeType(ph_type, phonemes[i]))
                {
                    var f = new Regex(ph_type);
                    format = f.Replace(format, phonemes[i], 1);
                    i++;
                }
                else throw new Exception($"Wrong phonemes [ {string.Join(" ", phonemes)} ] to format alias {alias_type} for {Singer.Current.VoicebankType}");
            }

            return format;
        }

        public string AliasReplace(string line)
        {
            foreach (string[] pair in AliasReplaces)
            {
                try
                {
                    var pattern = pair[0];
                    var replacement = pair[1];
                    if (Regex.IsMatch(line, pattern))
                        line = Regex.Replace(line, pattern, replacement);
                    //if (line.Contains(pair[0])) line = line.Replace(pair[0], pair[1]);
                }
                catch (Exception ex)
                {
                    Errors.ErrorMessage(ex, "Error on AliasReplace");
                }
            }
            return line;
        }

        public string PhonemeReplace(string phonemes)
        {
            foreach (string[] pair in PhonemeReplaces)
            {
                var pattern = pair[0];
                var replacement = pair[1];
                if (Regex.IsMatch(phonemes, pattern))
                    phonemes = Regex.Replace(phonemes, pattern, replacement);
            }
            return phonemes;
        }

        public Syllable PhonemeReplace(Syllable syllable)
        {
            var phonemes = PhonemeReplace(syllable.ToString());
            return new Syllable(phonemes.Split(' '), this);
        }

        public string[] DictAnalysis(string lyric)
        {
            // DEPRECATED
            List<string> aliases = new List<string>();
            int k;
            string l = "";
            List<string> syll = new List<string>();
            for (int i = 0; i < lyric.Length; )
            {
                for (k = lyric.Length - i; k > 0; k--)
                {
                    l = lyric.Substring(i, k);
                    if (Dict.Has(l))
                        break;
                    var last = l.Last().ToString();
                    if (Dict.Has(last) && Dict.Get(last)[0] == "#SKIP")
                        k--;
                }
                if (k == 0)
                {
                    return new[] { lyric };
                }
                else
                {
                    aliases.AddRange(Dict.Get(l));
                    i += k;
                }
            }
            int vs = aliases.Select(n => GetAliasType(n) != "C").ToArray().Length;
            if (vs == 1)
                return new[] { string.Join(" ", Dict.Get(l)) };
            var sylls = new List<string>();
            int lastv = aliases.Select(n => GetAliasType(n) != "C").ToList().FindLastIndex(n => n);
            int prevv = -1;
            for (int ph = 0; ph <= lastv; ph++)
            {
                string alias_type = GetAliasType(aliases[ph]);
                while (alias_type == "C" && ph < lastv)
                {
                    ph++;
                    alias_type = GetAliasType(aliases[ph]);
                }
                if (ph == lastv)
                {
                    sylls.Add(string.Join(" ", aliases.ToList().GetRange(prevv + 1, aliases.Count - 1 - prevv)));
                    prevv = ph;
                }
                else
                {
                    sylls.Add(string.Join(" ", aliases.ToList().GetRange(prevv + 1, ph - prevv)));
                    prevv = ph;
                }
            }
            string t = string.Join(" ", sylls);
            return sylls.ToArray();
        }

        public string ValidateLyric(string lyric)
        {
            if (!IsLoaded)
                return lyric;
            if (IsRest(lyric))
                return " ";
            if (lyric == "rr")
                return "r";
            else
                return lyric;
        }

        public int FindVowel(string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                if (IsVowel(aliases[i])) return i;
            }
            return 0;
        }

        public RuleResult GetRuleResult(Format format, string lyricPrev, string lyric)
        {
            string[] phonemesPrev = GetPhonemes(lyricPrev);
            string[] phonemes = GetPhonemes(lyric);
            string[] phonemesNew = format.GetNewPhonemes(phonemesPrev, phonemes);
            string alias = GetAlias(format.AliasType, phonemesNew);
            RuleResult ruleResult = new RuleResult(alias, format.AliasType);
            return ruleResult;
        }

        public int VowelsCount(string[] aliases)
        {
            return aliases.Count(n => new[] { "CV", "V", "-CV", "`V", "-V"}.Contains(GetAliasType(n)));
        }

        public bool AddWord(string word, string phonemes)
        {
            if (HasDict)
            {
                string line = word + "=" + phonemes;
                string old_phonemes = "";
                bool wasInDict = Dict.Has(word);
                if (wasInDict)
                    old_phonemes = string.Join(" ", Dict.Get(word));
                if (Dict.Add(line))
                {
                    if (!wasInDict || old_phonemes != phonemes)
                    {
                        WriteWord(line, wasInDict);
                    }
                    return true;
                }
            }
            return false;
        }

        public List<Syllable> GetSyllables(string[] phonemes)
        {
            var sylls = new List<Syllable>();
            var syll_phonemes = new List<string>();
            int i = 0;

            // non-vowel or 1 vowel word
            if (!phonemes.Any(n => IsVowel(n)) || phonemes.Count(n => IsVowel(n)) == 1)
                return new[] { new Syllable(phonemes, this) }.ToList();

            for (; i < phonemes.Length;)
            {
                // add beginning CC
                for (; IsConsonant(phonemes[i]); i++)
                    syll_phonemes.Add(phonemes[i]);

                // make last syll if only 1 vowel left
                var rest = phonemes.ToList().GetRange(i, phonemes.Length - i);
                if (rest.Count(n => IsVowel(n)) == 1)
                    for (; i < phonemes.Length; i++)
                        syll_phonemes.Add(phonemes[i]);
                else
                {
                    // add vowels itself
                    syll_phonemes.Add(phonemes[i]);
                    i++;

                    // add as [C*VC] [CV...] if there is more than 1 consonant between two vowels
                    if (i + 2 < phonemes.Length && IsConsonant(phonemes[i]) && IsConsonant(phonemes[i + 1]))
                    {
                        syll_phonemes.Add(phonemes[i]);
                        i++;
                    }
                }

                sylls.Add(new Syllable(syll_phonemes, this));
                syll_phonemes = new List<string>();
            }
            return sylls;
        }

        #region private

        private void WriteWord(string line, bool wasInDict)
        {
            var dictPath = GetDictPath();
            if (wasInDict)
            {
                // Better not
                try
                {
                    File.AppendAllText(dictPath, line + "\r\n");
                }
                catch (Exception ex)
                {
                    Errors.ErrorMessage(ex, "Cant modify dict file");
                }
            }
            else
            {
                try
                {
                    File.AppendAllText(dictPath, line + "\r\n");
                }
                catch (Exception ex)
                {
                    Errors.ErrorMessage(ex, "Cant modify dict file");
                }
            }
        }
        #endregion
    }
}
