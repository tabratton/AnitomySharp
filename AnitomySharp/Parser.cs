/*
 * Copyright (c) 2014-2017, Eren Okka
 * Copyright (c) 2016-2017, Paul Miller
 * Copyright (c) 2017-2018, Tyler Bratton
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace AnitomySharp
{
  /// <summary>
  /// Class to classify <see cref="Token"/>s
  /// </summary>
  public class Parser
  {
    public bool IsEpisodeKeywordsFound { get; set; } = false;
    public ParserHelper ParseHelper { get; }
    public ParserNumber ParseNumber { get; }
    public List<Element> Elements { get; }
    public List<Token> Tokens { get; }
    public Options Options { get; }

    /// <summary>
    /// Constructs a new token parser
    /// </summary>
    /// <param name="elements">the list where parsed elements will be added</param>
    /// <param name="options">the parser options</param>
    /// <param name="tokens">the list of tokens</param>
    public Parser(List<Element> elements, Options options, List<Token> tokens)
    {
      this.Elements = elements;
      this.Options = options;
      this.Tokens = tokens;
      this.ParseHelper = new ParserHelper(this);
      this.ParseNumber = new ParserNumber(this);
    }

    /** Begins the parsing process */
    public bool Parse()
    {
      SearchForKeywords();
      SearchForIsolatedNumbers();

      if (Options.ParseEpisodeNumber)
      {
        SearchForEpisodeNumber();
      }

      SearchForAnimeTitle();

      if (Options.ParseReleaseGroup && Empty(Element.ElementCategory.ElementReleaseGroup))
      {
        SearchForReleaseGroup();
      }

      if (Options.ParseEpisodeTitle && !Empty(Element.ElementCategory.ElementEpisodeNumber))
      {
        SearchForEpisodeTitle();
      }

      ValidateElements();
      return Empty(Element.ElementCategory.ElementAnimeTitle);
    }

    /** Search for anime keywords. */
    private void SearchForKeywords()
    {
      for (var i = 0; i < Tokens.Count; i++)
      {
        var token = Tokens[i];
        if (token.Category != Token.TokenCategory.Unknown) continue;

        var word = token.Content;
        word = word.Trim(" -".ToCharArray());
        if (string.IsNullOrEmpty(word)) continue;

        // Don't bother if the word is a number that cannot be CRC
        if (word.Length != 8 && StringHelper.IsNumericString(word)) continue;

        var keyword = KeywordManager.Normalize(word);
        var category = Element.ElementCategory.ElementUnknown;
        var options = new KeywordOptions();

        if (KeywordManager.Instance.FindAndSet(keyword, ref category, ref options))
        {
          if (!Options.ParseReleaseGroup && category == Element.ElementCategory.ElementReleaseGroup) continue;
          if (!ParseHelper.IsElementCategorySearchable(category) || !options.Searchable) continue;
          if (ParseHelper.IsElementCategorySingular(category) && !Empty(category)) continue;
          if (category == Element.ElementCategory.ElementAnimeSeasonPrefix)
          {
            ParseHelper.CheckAndSetAnimeSeasonKeyword(token, i);
            continue;
          }
          else if (category == Element.ElementCategory.ElementEpisodePrefix)
          {
            if (options.Valid)
            {
              ParseHelper.CheckExtentKeyword(Element.ElementCategory.ElementEpisodeNumber, i, token);
              continue;
            }
          }
          else if (category == Element.ElementCategory.ElementReleaseVersion)
          {
            word = word.Substring(1);
          }
          else if (category == Element.ElementCategory.ElementVolumePrefix)
          {
            ParseHelper.CheckExtentKeyword(Element.ElementCategory.ElementVolumeNumber, i, token);
            continue;
          }
        }
        else
        {
          if (Empty(Element.ElementCategory.ElementFileChecksum) && ParserHelper.IsCrc32(word))
          {
            category = Element.ElementCategory.ElementFileChecksum;
          } else if (Empty(Element.ElementCategory.ElementVideoResolution) && ParserHelper.IsResolution(word))
          {
            category = Element.ElementCategory.ElementVideoResolution;
          }
        }

        if (category != Element.ElementCategory.ElementUnknown)
        {
          Elements.Add(new Element(category, word));
          if (options != null && options.Identifiable)
          {
            token.Category = Token.TokenCategory.Identifier;
          }
        }
      }
    }

    /** Search for episode number. */
    private void SearchForEpisodeNumber()
    {
      // List all unknown tokens that contain a number
      List<Result> tokens = new List<Result>();
      for (var i = 0; i < Tokens.Count; i++)
      {
        var token = Tokens[i];
        if (token.Category == Token.TokenCategory.Unknown &&
            ParserHelper.IndexOfFirstDigit(token.Content) != -1)
        {
          tokens.Add(new Result(token, i));
        }
      }

      if (tokens.Count == 0) return;

      IsEpisodeKeywordsFound = !Empty(Element.ElementCategory.ElementEpisodeNumber);

      // If a token matches a known episode pattern, it has to be the episode number
      if (ParseNumber.SearchForEpisodePatterns(tokens)) return;

      // We have previously found an episode number via keywords
      if (!Empty(Element.ElementCategory.ElementEpisodeNumber)) return;

      // From now on, we're only interested in numeric tokens
      tokens.RemoveAll(r => !StringHelper.IsNumericString(r.Token.Content));

      // e.g. "01 (176)", "29 (04)"
      if (ParseNumber.SearchForEquivalentNumbers(tokens)) return;

      // e.g. " - 08"
      if (ParseNumber.SearchForSeparatedNumbers(tokens)) return;

      // "e.g. "[12]", "(2006)"
      if (ParseNumber.SearchForIsolatedNumbers(tokens)) return;

      // Consider using the last number as a last resort
      ParseNumber.SearchForLastNumber(tokens);
    }

    /// <summary>
    /// Search for anime title
    /// </summary>
    private void SearchForAnimeTitle()
    {
      var enclosedTitle = false;

      var tokenBegin = Token.FindToken(Tokens, Token.TokenFlag.FlagNotEnclosed, Token.TokenFlag.FlagUnknown);

      // If that doesn't work, find the first unknown token in the second enclosed
      // group, assuming that the first one is the release group
      if (tokenBegin.Token == null)
      {
        tokenBegin = new Result(null, 0);
        enclosedTitle = true;
        var skippedPreviousGroup = false;

        do
        {
          tokenBegin = Token.FindToken(Tokens, tokenBegin, Token.TokenFlag.FlagUnknown);
          if (tokenBegin.Token == null) break;

          // Ignore groups that are composed of non-Latin characters
          if (StringHelper.IsMostlyLatinString(tokenBegin.Token.Content) && skippedPreviousGroup)
          {
            break;
          }

          // Get the first unknown token of the next group
          tokenBegin = Token.FindToken(Tokens, tokenBegin, Token.TokenFlag.FlagBracket);
          tokenBegin = Token.FindToken(Tokens, tokenBegin, Token.TokenFlag.FlagUnknown);
          skippedPreviousGroup = true;
        } while (tokenBegin.Token != null);
      }

      if (tokenBegin.Token == null) return;

      // Continue until an identifier (or a bracket, if the title is enclosed) is found
      var tokenEnd = Token.FindToken(
        Tokens,
        tokenBegin,
        Token.TokenFlag.FlagIdentifier,
        enclosedTitle ? Token.TokenFlag.FlagBracket : Token.TokenFlag.FlagNone);

      // If within the interval there's an open bracket without its matching pair,
      // move the upper endpoint back to the bracket
      if (!enclosedTitle)
      {
        var end = tokenEnd.Pos != null ? tokenEnd.Pos.Value : Tokens.Count;
        var lastBracket = tokenEnd;
        var bracketOpen = false;
        for (var i = tokenBegin.Pos; i < end; i++)
        {
          var token = Tokens[i.Value];
          if (token.Category == Token.TokenCategory.Bracket)
          {
            lastBracket = new Result(token, i);
            bracketOpen = !bracketOpen;
          }
        }

        if (bracketOpen) tokenEnd = lastBracket;
      }

      // If the interval ends with an enclosed group (e.g. "Anime Title [Fansub]"),
      // move the upper endpoint back to the beginning of the group. We ignore
      // parenthese in order to keep certain groups (e.g. "(TV)") intact.
      if (!enclosedTitle)
      {
        var end = tokenEnd.Pos != null ? tokenEnd.Pos.Value : Tokens.Count;
        var token = Token.FindPrevToken(Tokens, end, Token.TokenFlag.FlagNotDelimiter);

        while (ParserHelper.IsTokenCategory(token.Token, Token.TokenCategory.Bracket) && token.Token.Content[0] != ')')
        {
          token = Token.FindPrevToken(Tokens, token, Token.TokenFlag.FlagBracket);
          if (token.Pos != null)
          {
            tokenEnd = token;
            token = Token.FindPrevToken(Tokens, tokenEnd, Token.TokenFlag.FlagNotDelimiter);
          }
        }
      }

      var endPos = Tokens.Count;
      if (tokenEnd.Token != null) endPos = Math.Min(tokenEnd.Pos.Value, endPos);
      ParseHelper.BuildElement(Element.ElementCategory.ElementAnimeTitle, false, Tokens.GetRange(tokenBegin.Pos.Value, endPos - tokenBegin.Pos.Value));
    }

    /// <summary>
    /// Search for release group
    /// </summary>
    private void SearchForReleaseGroup()
    {
      for (Result tokenBegin = new Result(null, 0), tokenEnd = tokenBegin;
        tokenBegin.Pos != null && tokenBegin.Pos.Value < Tokens.Count;)
      {
        // Find the first enclosed unknown token
        tokenBegin = Token.FindToken(Tokens, tokenEnd, Token.TokenFlag.FlagEnclosed, Token.TokenFlag.FlagUnknown);
        if (tokenBegin.Token == null) return;

        // Continue until a bracket or identifier is found
        tokenEnd = Token.FindToken(Tokens, tokenBegin, Token.TokenFlag.FlagBracket, Token.TokenFlag.FlagIdentifier);
        if (tokenEnd.Token == null || tokenEnd.Token.Category != Token.TokenCategory.Bracket) continue;

        // Ignore if it's not the first non-delimiter token in group
        var prevToken = Token.FindPrevToken(Tokens, tokenBegin, Token.TokenFlag.FlagNotDelimiter);
        if (prevToken.Token != null && prevToken.Token.Category != Token.TokenCategory.Bracket) continue;

        var end = Tokens.Count;
        end = Math.Min(tokenEnd.Pos.Value, end);
        ParseHelper.BuildElement(Element.ElementCategory.ElementReleaseGroup, true, Tokens.GetRange(tokenBegin.Pos.Value, end - tokenBegin.Pos.Value));
        return;
      }
    }

    /// <summary>
    /// Search for episode title
    /// </summary>
    private void SearchForEpisodeTitle()
    {
      // Find the first non-enclosed unknown token
      var tokenBegin = Token.FindToken(Tokens, Token.TokenFlag.FlagNotEnclosed, Token.TokenFlag.FlagUnknown);
      if (tokenBegin.Token == null) return;

      // Continue until a bracket or identifier is found
      var tokenEnd = Token.FindToken(Tokens, tokenBegin, Token.TokenFlag.FlagBracket, Token.TokenFlag.FlagIdentifier);

      var end = Tokens.Count;
      if (tokenEnd.Pos != null) end = Math.Min(tokenEnd.Pos.Value, end);
      ParseHelper.BuildElement(Element.ElementCategory.ElementEpisodeTitle, false, Tokens.GetRange(tokenBegin.Pos.Value, end - tokenBegin.Pos.Value));
    }

    /// <summary>
    /// Search for isolated numbers
    /// </summary>
    private void SearchForIsolatedNumbers()
    {
      for (var i = 0; i < Tokens.Count; i++)
      {
        var token = Tokens[i];
        if (token.Category != Token.TokenCategory.Unknown || !StringHelper.IsNumericString(token.Content) ||
            !ParseHelper.IsTokenIsolated(i))
        {
          continue;
        }

        var number = StringHelper.StringToInt(token.Content);

        // Anime year
        if (number >= ParserNumber.AnimeYearMin && number <= ParserNumber.AnimeYearMax)
        {
          if (Empty(Element.ElementCategory.ElementAnimeYear))
          {
            Elements.Add(new Element(Element.ElementCategory.ElementAnimeYear, token.Content));
            token.Category = Token.TokenCategory.Identifier;
            continue;
          }
        }

        // Video resolution
        if (number == 480 || number == 720 || number == 1080)
        {
          // If these numbers are isolated, it's more likely for them to be the
          // video resolution rather than the episode number. Some fansub groups use these without the "p" suffix.
          if (Empty(Element.ElementCategory.ElementVideoResolution))
          {
            Elements.Add(new Element(Element.ElementCategory.ElementVideoResolution, token.Content));
            token.Category = Token.TokenCategory.Identifier;
          }
        }
      }
    }

    /// <summary>
    /// Validate Elements
    /// </summary>
    private void ValidateElements()
    {
      if (!Empty(Element.ElementCategory.ElementAnimeType) && !Empty(Element.ElementCategory.ElementEpisodeTitle))
      {
        var episodeTitle = Get(Element.ElementCategory.ElementEpisodeTitle);

        for (var i = 0; i < Elements.Count;)
        {
          var el = Elements[i];

          if (el.Category == Element.ElementCategory.ElementAnimeType)
          {
            if (episodeTitle.Contains(el.Value))
            {
              if (episodeTitle.Length == el.Value.Length)
              {
                Elements.RemoveAll(element =>
                  element.Category == Element.ElementCategory.ElementEpisodeTitle); // invalid episode title
              }
              else
              {
                var keyword = KeywordManager.Normalize(el.Value);
                if (KeywordManager.Instance.Contains(Element.ElementCategory.ElementAnimeType, keyword))
                {
                  i = Erase(el); // invalid anime type
                  continue;
                }
              }
            }
          }

          ++i;
        }
      }
    }

    /// <summary>
    /// Returns whether or not the parser contains this category
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    private bool Empty(Element.ElementCategory category)
    {
      return Elements.All(element => element.Category != category);
    }

    /// <summary>
    /// Returns the value of a particular category
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    private string Get(Element.ElementCategory category)
    {
      var foundElement = Elements.Find(element => element.Category == category);

      if (foundElement == null)
      {
        Element e = new Element(category, "");
        Elements.Add(e);
        foundElement = e;
      }

      return foundElement.Value;
    }

    /// <summary>
    /// Deletes the first element with the same <code>element.Category</code> and returns the deleted element's position.
    /// </summary>
    private int Erase(Element element)
    {
      var removedIdx = -1;
      for (var i = 0; i < Elements.Count; i++)
      {
        var currentElement = Elements[i];
        if (element.Category == currentElement.Category)
        {
          removedIdx = i;
          Elements.RemoveAt(i);
          break;
        }
      }

      return removedIdx;
    }
  }
}