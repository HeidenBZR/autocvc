﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using App.Classes;

namespace App
{
    class Parser
    {
        public static void Split()
        {
            int i = Ust.Notes.First().Number == Number.PREV ? 2 : 1; 
            int stop = Ust.Notes.Last().Number == Number.NEXT ? 1 : 0;
            for (; i < Ust.Notes.Length - stop; i++)
            {
                UNote note = Ust.Notes[i];
                UNote prev = i > 0 ? Ust.Notes[i - 1] : null;
                UNote next = i + 1 < Ust.Notes.Length ? Ust.Notes[i + 1] : null;
                var members = note.ParsedLyric.Trim().Split(' ');
                int inserted = 0;
                if (members.Length > 1)
                {
                    int vowelI = members.ToList().FindIndex(n => Atlas.GetAliasType(n) != "C");
                    note.ParsedLyric = members[vowelI];
                    if (vowelI > 0)
                    {
                        for (int k = vowelI - 1; k >= 0; k--)
                        {
                            if (prev != null)
                                if (prev.IsRest())
                                    Ust.InsertNote(prev, members[k], Insert.After, note);
                                else
                                    Ust.InsertNote(prev, members[k], Insert.After, prev);
                            else { }
                            inserted++;
                        }
                    }
                    i += inserted;
                    if (vowelI < members.Length - 1)
                    {
                        if (Ust.Notes[i + 1].IsRest())
                        {
                            for (int k = members.Length - 1; k > vowelI; k--)
                            {
                                Ust.InsertNote(note, members[k], Insert.After, note, next);
                                i++;
                            }
                        }
                        else
                        {
                            for (int k = members.Length - 1; k > vowelI; k--)
                            {
                                Ust.InsertNote(note, members[k], Insert.After, note);
                                i++;
                            }
                        }
                    }
                }
            }
        }

        public static void AtlasConverting()
        {
            //int i = Ust.Notes.First().Number == Number.PREV ? 1 : 0;
            int i = 1; /// Всегда нужна предыдущая нота, а первая не обрабатывается
            int stop = Ust.Notes.Last().Number == Number.NEXT ? 1 : 0;
            for  (; i < Ust.Notes.Length - stop; i++)
            {
                string lyric = Ust.Notes[i].ParsedLyric;
                string lyricPrev = Ust.Notes[i - 1].ParsedLyric;
                string aliasType = "";
                string aliasTypePrev = "";
                bool tookAliases = false;
                try
                {
                    aliasType = Atlas.GetAliasType(lyric);
                    aliasTypePrev = Atlas.GetAliasType(lyricPrev);
                    tookAliases = true;
                }
                catch (KeyNotFoundException ex) { Program.Log(ex.Message); }
                if (!tookAliases) { i++; continue; }

                if (lyricPrev == "b'e" && lyric == "po")
                    Console.WriteLine();

                Classes.Rule rule = Classes.Rule.GetRule($"{aliasTypePrev},{aliasType}");
                if (rule == null)
                {
                    Ust.Notes[i].ParsedLyric = lyric;
                    continue;
                }

                if (rule.MustConvert)
                {
                    RuleResult result = rule.FormatConvert.GetResult(lyricPrev, lyric);
                    Ust.Notes[i].ParsedLyric = result.Alias;
                    if (Ust.Notes[i].Parent != null)
                    {
                        Oto oto = Singer.Current.FindOto(Ust.Notes[i].ParsedLyric);
                        if (oto != null)
                        {
                            int ilength = MusicMath.MillisecondToTick(oto.InnerLength, Ust.Tempo);
                            Ust.Notes[i].Length += ilength;
                            Ust.Notes[i].Parent.Length -= ilength;
                        }
                    }
                }
                else Ust.Notes[i].ParsedLyric = lyric;
                Console.WriteLine(Ust.Notes[i].ParsedLyric);
                if (rule.MustInsert)
                {
                    RuleResult result = rule.FormatInsert.GetResult(lyricPrev, lyric);
                    Insert insert =  new[] { "V-", "-C" }.Contains(result.AliasType) ? Insert.Before : Insert.After;
                    UNote pitchparent = new[] { "-C" }.Contains(result.AliasType) ? Ust.Notes[i] : Ust.Notes[i - 1];
                    UNote parent = new[] { "VC"}.Contains(result.AliasType) ? Ust.Notes[i - 1] : Ust.Notes[i];
                    if (PluginWindow.MakeVR || result.AliasType != "V-")
                    {
                        if (Ust.InsertNote(parent, result.Alias, insert, pitchparent))
                        {
                            Console.WriteLine(Ust.Notes[i].ParsedLyric);
                        }
                    }
                    if (new[] { "-C" }.Contains(result.AliasType))
                        i++;
                }
            }
        }

        public static void ToCV()
        {
            ToCVC();

            bool c_left = true;
            while (c_left)
            {
                c_left = false;
                int i = Ust.Notes.First().Number == Number.PREV ? 1 : 0;
                int stop = Ust.Notes.Last().Number == Number.NEXT ? 1 : 0;
                for (; i < Ust.Notes.Length - stop; i++)
                {
                    // remove C
                    var members = Ust.Notes[i].ParsedLyric.Split(' ');
                    bool doit = members.All(n => Atlas.GetAliasType(n) == "C");
                    if (doit)
                    {
                        c_left = true;
                        if (i > 0 && Ust.Notes[i - 1].IsRest())
                        {
                            /// R [-C] -CV
                            if (Ust.Notes[i + 1].ParsedLyric == Number.DELETE)
                            {
                                /// R [-C] -C -CV
                                Ust.Notes[i + 1].ParsedLyric = Ust.Notes[i].ParsedLyric;
                                Ust.Notes[i + 1].Length = Ust.Notes[i].Length;
                            }
                            else
                            {
                                Ust.Notes[i + 1].ParsedLyric = $"{Ust.Notes[i].ParsedLyric} {Ust.Notes[i + 1].ParsedLyric}";
                                Ust.Notes[i - 1].Length += Ust.Notes[i].Length;
                            }
                            Ust.Notes[i].ParsedLyric = Number.DELETE;
                            Ust.Notes[i].Number = Number.DELETE;
                            Ust.Notes[i].Length = 0;
                        }
                        else if (Ust.Notes[i + 1].IsRest())
                        {
                            /// VC- [C] R
                            if (Ust.Notes[i - 1].ParsedLyric == Number.DELETE)
                            {
                                /// VC- C [C] R
                                Ust.Notes[i - 1].ParsedLyric = Ust.Notes[i].ParsedLyric;
                                Ust.Notes[i - 1].Length = Ust.Notes[i].Length;
                            }
                            else
                            {
                                Ust.Notes[i - 1].ParsedLyric += $" {Ust.Notes[i].ParsedLyric}";
                                Ust.Notes[i + 1].Length += Ust.Notes[i].Length;
                            }
                            Ust.Notes[i].ParsedLyric = Number.DELETE;
                            Ust.Notes[i].Number = Number.DELETE;
                            Ust.Notes[i].Length = 0;
                        }
                        else if (i > 0 && Ust.Notes[i - 1].ParsedLyric.Split(' ').Any(n => Atlas.GetAliasType(n).Contains("V")))
                        {
                            /// CV [(VC-)] (-CV)
                            Ust.Notes[i - 1].Length += Ust.Notes[i].Length;
                            Ust.Notes[i + 1].ParsedLyric = $"{Ust.Notes[i].ParsedLyric} {Ust.Notes[i + 1].ParsedLyric}";
                            Ust.Notes[i].ParsedLyric = Number.DELETE;
                            Ust.Notes[i].Number = Number.DELETE;
                            Ust.Notes[i].Length = 0;
                        }
                        else
                        {
                            int k = 1;
                            while (Ust.Notes[i - k].Number == Number.DELETE) k++;
                            if (Ust.Notes[i - k].IsRest())
                            {
                                /// (R) (С)* [C] (!R)
                                Ust.Notes[i - k].Length += Ust.Notes[i].Length;
                                Ust.Notes[i + 1].ParsedLyric = $"{Ust.Notes[i].ParsedLyric} {Ust.Notes[i + 1].ParsedLyric}";
                                Ust.Notes[i].ParsedLyric = Number.DELETE;
                                Ust.Notes[i].Number = Number.DELETE;
                                Ust.Notes[i].Length = 0;
                            }
                            else
                            {
                                /// (CV) (VC-) (С)* [C] (!R)
                                Ust.Notes[i - k].Length += Ust.Notes[i].Length;
                                Ust.Notes[i - k].ParsedLyric = $"{Ust.Notes[i - k].ParsedLyric} {Ust.Notes[i].ParsedLyric}";
                                Ust.Notes[i].ParsedLyric = Number.DELETE;
                                Ust.Notes[i].Number = Number.DELETE;
                                Ust.Notes[i].Length = 0;
                            }
                        }
                    }
                }

            }
        }

        public static void ToCVC()
        {
            int i = Ust.Notes.First().Number == Number.PREV ? 1 : 0;
            int stop = Ust.Notes.Last().Number == Number.NEXT ? 1 : 0;
            for (; i < Ust.Notes.Length - stop; i++)
            {
                UNote note = Ust.Notes[i];
                switch (Atlas.GetAliasType(note.ParsedLyric))
                {
                    case "-V":
                        note.ParsedLyric = Atlas.GetAlias("V", Atlas.GetPhonemes(note.ParsedLyric));
                        break;
                    case "`V":
                        note.ParsedLyric = Atlas.GetAlias("V", Atlas.GetPhonemes(note.ParsedLyric));
                        break;
                    case "V`":
                        if (i > 0)
                        {
                            Ust.Notes[i - 1].Length += note.Length;
                            note.ParsedLyric = Number.DELETE;
                            note.Number = Number.DELETE;
                        }
                        break;
                    case "V-":
                        note.ParsedLyric = "R";
                        UNote container = Ust.GetNextNote(note);
                        if (container != null || container.IsRest())
                        {
                            container.Length += note.Length;
                            note.ParsedLyric = Number.DELETE;
                            note.Number = Number.DELETE;
                        }
                        break;
                    case "-C":
                        note.ParsedLyric = Atlas.GetAlias("C", Atlas.GetPhonemes(note.ParsedLyric));
                        break;
                    case "-CV":
                        note.ParsedLyric = Atlas.GetAlias("CV", Atlas.GetPhonemes(note.ParsedLyric));
                        break;
                    case "VC-":
                        note.ParsedLyric = Atlas.GetAlias("C", new[] { Atlas.GetPhonemes(note.ParsedLyric).ToList().Last() });
                        break;
                    case "VC":
                        if (i > 0)
                        {
                            Ust.Notes[i - 1].Length += note.Length;
                            note.ParsedLyric = Number.DELETE;
                            note.Number = Number.DELETE;
                        }
                        break;
                }

            }

        }
    }
}
