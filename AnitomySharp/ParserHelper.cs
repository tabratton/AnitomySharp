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
using System.Text;

namespace AnitomySharp
{

  /// <summary>
  /// Utility class to assist in the parsing.
  /// </summary>
  public class ParserHelper
  {
    public static readonly string Dashes = "-\u2010\u2011\u2012\u2013\u2014\u2015";
    public static readonly string DashesWithSpace = " -\u2010\u2011\u2012\u2013\u2014\u2015";
    public static readonly Dictionary<string, string> Ordinals = new Dictionary<string, string>
    {
      {"1st", "1"}, {"First", "1"},
      {"2nd", "2"}, {"Second", "2"},
      {"3rd", "3"}, {"Third", "3"},
      {"4th", "4"}, {"Fourth", "4"},
      {"5th", "5"}, {"Fifth", "5"},
      {"6th", "6"}, {"Sixth", "6"},
      {"7th", "7"}, {"Seventh", "7"},
      {"8th", "8"}, {"Eighth", "8"},
      {"9th", "9"}, {"Ninth", "9"}
    };

    private readonly Parser _parser;

    public ParserHelper(Parser parser)
    {
      _parser = parser;
    }

    /// <summary>
    /// Returns whether or not the <code>result</code> matches the <code>category</code>.
    /// </summary>
    public static bool IsTokenCategory(Result result, Token.TokenCategory category)
    {
      return result != null && result.Token != null && result.Token.Category == category;
    }

    /// <summary>
    /// Returns whether or not the <code>token</code> matches the <code>category</code>.
    /// </summary>
    public static bool IsTokenCategory(Token token, Token.TokenCategory category)
    {
      return token != null && token.Category == category;
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a CRC string.
    /// </summary>
    public static bool IsCrc32(string str)
    {
      return str != null && str.Length == 8 && StringHelper.IsHexadecimalString(str);
    }

    /// <summary>
    /// Returns whether or not the <code>character</code> is a dash character
    /// </summary>
    public static bool IsDashCharacter(char c)
    {
      return Dashes.Contains(c.ToString());
    }

    /// <summary>
    /// Returns a number from an original (e.g. 2nd)
    /// </summary>
    public static string GetNumberFromOrdinal(string str)
    {
      if (string.IsNullOrEmpty(str)) return "";
      return Ordinals.TryGetValue(str, out var foundString) ? foundString : "";
    }

    /// <summary>
    /// Returns the index of the first digit in the <code>str</code>; -1 otherwise.
    /// </summary>
    public static int IndexOfFirstDigit(string str)
    {
      if (string.IsNullOrEmpty(str)) return -1;
      for (var i = 0; i < str.Length; i++)
      {
        if (char.IsDigit(str, i))
        {
          return i;
        }
      }
      return -1;
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a resolution.
    /// </summary>
    public static bool IsResolution(string str)
    {
      if (string.IsNullOrEmpty(str)) return false;
      const int minWidthSize = 3;
      const int minHeightSize = 3;

      if (str.Length >= minWidthSize + 1 + minHeightSize)
      {
        var pos = str.IndexOfAny("xX\u00D7".ToCharArray());
        if (pos != -1 && pos >= minWidthSize && pos <= str.Length - (minHeightSize + 1))
        {
          for (var i = 0; i < str.Length; i++)
          {
            if (i != pos && !char.IsDigit(str[i])) return false;
          }

          return true;
        }
      }
      else if (str.Length >= minHeightSize + 1)
      {
        if (char.ToLower(str[str.Length - 1]) == 'p')
        {
          for (var i = 0; i < str.Length - 1; i++)
          {
            if (!char.IsDigit(str[i])) return false;
          }

          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Returns whether or not the <code>category</code> is searchable.
    /// </summary>
    public bool IsElementCategorySearchable(Element.ElementCategory category)
    {
      switch (category)
      {
        case Element.ElementCategory.ElementAnimeSeasonPrefix:
        case Element.ElementCategory.ElementAnimeType:
        case Element.ElementCategory.ElementAudioTerm:
        case Element.ElementCategory.ElementDeviceCompatibility:
        case Element.ElementCategory.ElementEpisodePrefix:
        case Element.ElementCategory.ElementFileChecksum:
        case Element.ElementCategory.ElementLanguage:
        case Element.ElementCategory.ElementOther:
        case Element.ElementCategory.ElementReleaseGroup:
        case Element.ElementCategory.ElementReleaseInformation:
        case Element.ElementCategory.ElementReleaseVersion:
        case Element.ElementCategory.ElementSource:
        case Element.ElementCategory.ElementSubtitles:
        case Element.ElementCategory.ElementVideoResolution:
        case Element.ElementCategory.ElementVideoTerm:
        case Element.ElementCategory.ElementVolumePrefix:
          return true;
      }

      return false;
    }

    /// <summary>
    /// Returns whether the <code>category</code> is singular.
    /// </summary>
    public bool IsElementCategorySingular(Element.ElementCategory category)
    {
      switch (category) {
        case Element.ElementCategory.ElementAnimeSeason:
        case Element.ElementCategory.ElementAnimeType:
        case Element.ElementCategory.ElementAudioTerm:
        case Element.ElementCategory.ElementDeviceCompatibility:
        case Element.ElementCategory.ElementEpisodeNumber:
        case Element.ElementCategory.ElementLanguage:
        case Element.ElementCategory.ElementOther:
        case Element.ElementCategory.ElementReleaseInformation:
        case Element.ElementCategory.ElementSource:
        case Element.ElementCategory.ElementVideoTerm:
          return false;
      }

      return true;
    }

    /// <summary>
    /// Returns whether or not a token at the current <code>pos</code> is isolated(surrounded by braces).
    /// </summary>
    public bool IsTokenIsolated(int pos)
    {
      Result prevToken = Token.FindPrevToken(_parser.Tokens, pos, Token.TokenFlag.FlagNotDelimiter);
      if (!IsTokenCategory(prevToken, Token.TokenCategory.Bracket)) return false;
      Result nextToken = Token.FindNextToken(_parser.Tokens, pos, Token.TokenFlag.FlagNotDelimiter);
      return IsTokenCategory(nextToken, Token.TokenCategory.Bracket);
    }

    /// <summary>
    /// Finds and sets the anime season keyword.
    /// </summary>
    public bool CheckAndSetAnimeSeasonKeyword(Token token, int currentTokenPos)
    {
      Action<Token, Token, string> setAnimeSeason = (first, second, content) =>
      {
        _parser.Elements.Add(new Element(Element.ElementCategory.ElementAnimeSeason, content));
        first.Category = Token.TokenCategory.Identifier;
        second.Category = Token.TokenCategory.Identifier;
      };

      Result previousToken = Token.FindPrevToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
      if (previousToken.Token != null)
      {
        var number = GetNumberFromOrdinal(previousToken.Token.Content);
        if (!string.IsNullOrEmpty(number))
        {
          setAnimeSeason(previousToken.Token, token, number);
          return true;
        }
      }

      Result nextToken = Token.FindNextToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
      if (nextToken.Token != null && StringHelper.IsNumericString(nextToken.Token.Content))
      {
        setAnimeSeason(token, nextToken.Token, nextToken.Token.Content);
        return true;
      }

      return false;
    }

    /// <summary>
    /// A Method to find the correct volume/episode number when prefixed (i.e. Vol.4).
    /// </summary>
    /// <param name="category">the category we're searching for</param>
    /// <param name="currentTokenPos">the current token position</param>
    /// <param name="token">the token</param>
    /// <returns>true if we found the volume/episode number</returns>
    public bool CheckExtentKeyword(Element.ElementCategory category, int currentTokenPos, Token token)
    {
      Result nToken = Token.FindNextToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
      if (IsTokenCategory(nToken.Token, Token.TokenCategory.Unknown))
      {
        if (IndexOfFirstDigit(nToken.Token.Content) == 0)
        {
          switch (category)
          {
            case Element.ElementCategory.ElementEpisodeNumber:
              if (!_parser.ParseNumber.MatchEpisodePatterns(nToken.Token.Content, nToken.Token))
              {
                _parser.ParseNumber.SetEpisodeNumber(nToken.Token.Content, nToken.Token, false);
              }
              break;
            case Element.ElementCategory.ElementVolumeNumber:
              if (!_parser.ParseNumber.MatchVolumePatterns(nToken.Token.Content, nToken.Token))
              {
                _parser.ParseNumber.SetVolumeNumber(nToken.Token.Content, nToken.Token, false);
              }
              break;
          }

          token.Category = Token.TokenCategory.Identifier;
          return true;
        }
      }

      return false;
    }


    public void BuildElement(Element.ElementCategory category, bool keepDelimiters, List<Token> tokens)
    {
      var element = new StringBuilder();

      for (var i = 0; i < tokens.Count; i++)
      {
        var token = tokens[i];
        switch (token.Category)
        {
          case Token.TokenCategory.Unknown:
            element.Append(token.Content);
            token.Category = Token.TokenCategory.Identifier;
            break;
          case Token.TokenCategory.Bracket:
            element.Append(token.Content);
            break;
          case Token.TokenCategory.Delimiter:
            string delimiter = "";
            if (!string.IsNullOrEmpty(token.Content))
            {
              delimiter = token.Content[0].ToString();
            }

            if (keepDelimiters)
            {
              element.Append(delimiter);
            } 
            else if (i > 0 && i < tokens.Count - 1)
            {
              switch (delimiter)
              {
                  case ",":
                  case "&":
                    element.Append(delimiter);
                    break;
                  default:
                    element.Append(' ');
                    break;
              }
            }
            break;
        }
      }

      if (!keepDelimiters)
      {
        element = new StringBuilder(element.ToString().Trim(DashesWithSpace.ToCharArray()));
      }

      if (!string.IsNullOrEmpty(element.ToString()))
      {
        _parser.Elements.Add(new Element(category, element.ToString()));
      }
    }
  }
}
