// Copyright (c) Bohdan Shtepan. All rights reserved.
// http://modern-dev.com/
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Convert;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
	public partial class Tokenizer
	{
		#region Class constructors

		protected Tokenizer(ParserOptions options, string input )
		{
			State = new State(options, input);
		}

		#endregion

		#region Class properties

		public State State { get; protected set; }

		private bool IsLookahead { get; set; }

		public string Input { get; protected set; }

		protected bool InModule { get; set; }

		public TokenContext CurrentContext => State.Context.Last();

		protected static Dictionary<string, TokenType> TT => TokenType.Types;

		#endregion

		#region Class methods

		private static string CodePointToString(int code)
			=> code <= 0xffff
				? ToChar(code).ToString()
				: $"{ToChar(((code - 0x10000) >> 10) + 0xD800)}{ToChar(((code - 0x10000) & 1023) + 0xDC00)}";

		/// <summary>
		/// Move to the next token
		/// </summary>
		protected void Next()
		{
			if (!IsLookahead)
			{
				State.Tokens.Add(new Token(State));
			}

			State.LastTokenEnd = State.End;
			State.LastTokenStart = State.Start;
			State.LastTokenEndLoc = State.EndLoc;
			State.LastTokenStartLoc = State.StartLoc;

			NextToken();
		}

		protected bool Eat(TokenType type)
		{
			if (Match(type))
			{
				Next();

				return true;
			}

			return false;
		}

		protected bool Match(TokenType type) => State.Type == type;

		protected virtual bool IsKeyword(string word) => Util.IsKeyword(word);

		protected State Lookahead()
		{
			var oldState = State;
			State = oldState.Clone(true);

			IsLookahead = true;

			Next();

			IsLookahead = false;

			var curr = State.Clone(true);
			State = oldState;

			return curr;
		}

		/// <summary>
		/// Toggle strict mode. Re-reads the next number or string to please
		/// pedantic tests (`"use strict"; 010;` should fail).
		/// </summary>
		/// <param name="strict"></param>
		protected void SetStrict(bool strict)
		{
			State.Strict = strict;

			if (!Match(TT["num"]) && !Match(TT["string"]))
			{
				return;
			}

			State.Position = State.Start;

			while (State.Position < State.LineStart)
			{
				State.LineStart = Input.LastIndexOf("\n", State.LineStart - 2, StringComparison.Ordinal) + 1;
				--State.CurLine;
			}

			NextToken();
		}

		/// <summary>
		///  Read a single token, updating the parser object's token-related properties.
		/// </summary>
		/// <returns></returns>
		protected TokenType NextToken()
		{
			var curContext = CurrentContext;

			if (curContext == null || !curContext.PreserveSpace)
			{
				SkipSpace();
			}

			State.ContainsOctal = false;
			State.OctalPosition = null;
			State.Start = State.Position;
			State.StartLoc = State.CurrentPosition;

			if (State.Position >= Input.Length)
			{
				return FinishToken(TT["eof"]);
			}

			return curContext?.Override != null ? curContext.Override(this) : ReadToken(FullCharCodeAtPos());
		}

		protected virtual TokenType ReadToken(int? code)
			=> IsIdentifierStart(code) || code == 92 ? ReadWord() : GetTokenFromCode(code ?? int.MaxValue);

		private int? FullCharCodeAtPos()
		{
			int? code = null;
			int? next = null;

			if (State.Position < Input.Length)
			{
				code = Input[State.Position];
			}

			if (code <= 0xd7ff || code >= 0xe000)
			{
				return code;
			}

			if (State.Position + 1 < Input.Length)
			{
				next = Input[State.Position + 1];
			}

			if (code.ToBool() && next.ToBool())
			{
				return (code << 10) + next - 0x35fdc00;
			}

			return null;
		}

		private void PushComment(bool block, string text, int start, int end, Position startLoc, Position endLoc)
		{
			var comment = new Node(start, startLoc)
			{
				Type = block ? "CommentBlock" : "CommentLine",
				Value = text,
				End = end,
				Loc = new SourceLocation(startLoc, endLoc)
			};


			if (!IsLookahead)
			{
				State.Tokens.Add(comment);
				State.Comments.Add(comment);
			}

			AddComment(comment);
		}

		private void SkipBlockComment()
		{
			var startLoc = State.CurrentPosition;
			var start = State.Position;
			var end = Input.IndexOf("*/", State.Position += 2, StringComparison.Ordinal);

			if (end == -1)
			{
				Raise(State.Position - 2, "Unterminated comment");
			}

			State.Position = end + 2;
			
			foreach(var match in LineBreak.Matches(Input, start).Cast<Match>().TakeWhile(m => m.Index < State.Position))
			{
				++State.CurLine;
				State.LineStart = match.Index + match.Length;
			}

			PushComment(true, Input.Slice(start + 2, end), start, State.Position, startLoc, State.CurrentPosition);
		}

		protected void SkipLineComment(int startSkip)
		{
			var start = State.Position;
			var startLoc = State.CurrentPosition;
			var ch = Input[State.Position += startSkip];

			while (State.Position < Input.Length && ch != 10 && ch != 13 && ch != 8232 && ch != 8233)
			{
				++State.Position;
				ch = Input[State.Position];
			}

			PushComment(false, Input.Slice(start + startSkip, State.Position), start, State.Position, startLoc,
				State.CurrentPosition);
		}

		/// <summary>
		/// Called at the start of the parse and after every token. Skips whitespace and comments, and.
		/// </summary>
		private void SkipSpace()
		{
			var done = false;

			while (State.Position < Input.Length)
			{
				if (done)
				{
					break;
				}

				int ch = Input[State.Position];

				switch (ch)
				{
					case 32:
					case 160:
					{
						++State.Position;

					}
						break;

					case 13:
					case 10:
					case 8232:
					case 8233:
					{
						if (ch == 13 && Input[State.Position + 1] == 10)
						{
							++State.Position;
						}

						++State.Position;
						++State.CurLine;
						State.LineStart = State.Position;
					}
						break;

					case 47:
					{
						switch ((int) Input[State.Position + 1])
						{
							case 42:
								SkipBlockComment();
								break;

							case 47:
								SkipLineComment(2);
								break;

							default:
								done = true;
								break;
						}
					}
						break;

					default:
					{
						if (ch > 8 && ch < 14 || ch >= 5760 && NonASCIIWhitespace.IsMatch(ToChar(ch).ToString()))
						{
							++State.Position;
						}
						else
						{
							done = true;
						}
					}
						break;
				}
			}
		}

		/// <summary>
		/// Called at the end of every token. Sets `end`, `val`, and
		/// maintains `context` and `exprAllowed`, and skips the space after
		/// the token, so that the next one's `start` will point at the
		/// right position.
		/// </summary>
		protected TokenType FinishToken(TokenType type, object val = null)
		{
			State.End = State.Position;
			State.EndLoc = State.CurrentPosition;

			var prevType = State.Type;

			State.Type = type;
			State.Value = val;

			UpdateContext(prevType);

			return null;
		}

		private TokenType ReadTokenDot()
		{
			var next = Input.CharCodeAt(State.Position + 1);

			if (next >= 48 && next <= 57)
			{
				return ReadNumber(true);
			}

			var next2 = Input.CharCodeAt(State.Position + 2);

			if (next == 46 && next2 == 46)
			{
				State.Position += 3;

				return FinishToken(TT["ellipsis"]);
			}

			++State.Position;

			return FinishToken(TT["dot"]);
		}

		private TokenType ReadTokenSlash()
		{
			if (State.ExprAllowed)
			{
				++State.Position;

				return ReadRegexp();
			}

			var next = Input.CharCodeAt(State.Position + 1);

			return next == 61 ? FinishOp(TT["assign"], 2) : FinishOp(TT["slash"], 1);
		}

		private TokenType ReadTokenMultModulo(int code)
		{
			var type = TT[code == 42 ? "star" : "modulo"];
			var width = 1;
			var next = Input.CharCodeAt(State.Position + 1);

			if (next == 42)
			{
				width++;
				next = Input.CharCodeAt(State.Position + 2);
				type = TT["exponent"];
			}

			if (next == 61)
			{
				width++;
				type = TT["assign"];
			}

			return FinishOp(type, width);
		}

		private TokenType ReadTokenPipeAmp(int code)
		{
			var next = Input.CharCodeAt(State.Position + 1);

			if (next == code)
			{
				return FinishOp(TT[code == 124 ? "logicalOR" : "logicalAND"], 2);
			}

			return next == 61 ? FinishOp(TT["assign"], 2) : FinishOp(TT[code == 124 ? "bitwiseOR" : "bitwiseAND"], 1);
		}

		private TokenType ReadTokenCaret()
		{
			var next = Input.CharCodeAt(State.Position + 1);

			return next == 61 ? FinishOp(TT["assign"], 2) : FinishOp(TT["bitwiseXOR"], 1);
		}

		private TokenType ReadTokenPlusMin(int code)
		{
			var next = Input.CharCodeAt(State.Position + 1);

			if (next == code)
			{
				if (next == 45 && Input.CharCodeAt(State.Position + 2) == 62 &&
					LineBreak.IsMatch(Input.Slice(State.LastTokenEnd, State.Position)))
				{
					SkipLineComment(3);
					SkipSpace();

					return NextToken();
				}

				return FinishOp(TT["incDec"], 2);
			}

			return next == 61 ? FinishOp(TT["assign"], 2) : FinishOp(TT["plusMin"], 1);
		}

		private TokenType ReadTokenLtGt(int code)
		{
			var next = Input.CharCodeAt(State.Position + 1);
			var size = 1;

			if (next == code)
			{
				size = code == 62 && Input.CharCodeAt(State.Position + 2) == 62 ? 3 : 2;

				return Input.CharCodeAt(State.Position + size) == 61 ? FinishOp(TT["assign"], size + 1) : FinishOp(TT["bitShift"], size);
			}

			if (next == 33 && code == 60 && Input.CharCodeAt(State.Position + 2) == 45 && Input.CharCodeAt(State.Position + 3) == 45)
			{
				if (InModule)
				{
					Unexpected();
				}

				SkipLineComment(4);
				SkipSpace();

				return NextToken();
			}

			if (next == 61)
			{
				size = Input.CharCodeAt(State.Position + 2) == 61 ? 3 : 2;
			}

			return FinishOp(TT["relational"], size);
		}

		private TokenType ReadTokenEqExcl(int code)
		{
			var next = Input.CharCodeAt(State.Position + 1);

			if (next == 61)
			{
				return FinishOp(TT["equality"], Input.CharCodeAt(State.Position + 2) == 61 ? 3 : 2);
			}

			if (code == 61 && next == 62)
			{
				State.Position += 2;

				return FinishToken(TT["arrow"]);
			}

			return FinishOp(TT[code == 61 ? "eq" : "prefix"], 1);
		}

		protected TokenType GetTokenFromCode(int code)
		{
			switch (code)
			{
				case 46:
					return ReadTokenDot();

				case 40:
					++State.Position;
					return FinishToken(TT["parenL"]);

				case 41:
					++State.Position;
					return FinishToken(TT["parenR"]);

				case 59:
					++State.Position;
					return FinishToken(TT["semi"]);

				case 44:
					++State.Position;
					return FinishToken(TT["comma"]);

				case 91:
					++State.Position;
					return FinishToken(TT["bracketL"]);

				case 93:
					++State.Position;
					return FinishToken(TT["bracketR"]);

				case 123:
					++State.Position;
					return FinishToken(TT["braceL"]);

				case 125:
					++State.Position;
					return FinishToken(TT["braceR"]);

				case 58:
					if (Input.CharCodeAt(State.Position + 1) == 58)
					{
						return FinishOp(TT["doubleColon"], 2);
					}

					++State.Position;

					return FinishToken(TT["colon"]);

				case 63:
					++State.Position;
					return FinishToken(TT["question"]);

				case 64:
					++State.Position;
					return FinishToken(TT["at"]);

				case 96:
					++State.Position;
					return FinishToken(TT["backQuote"]);

				case 48:
				case 49:
				case 50:
				case 51:
				case 52:
				case 53:
				case 54:
				case 55:
				case 56:
				case 57:
					if (code == 48)
					{
						var next = State.Position + 1 >= Input.Length ? int.MaxValue : Input[State.Position + 1];

						switch (next)
						{
							case 120:
							case 98:
								return ReadRadixNumber(16);
							case 111:
							case 79:
								return ReadRadixNumber(8);
						}

						if (next == 98 || next == 66)
						{
							return ReadRadixNumber(2);
						}
					}


					return ReadNumber(false);

				case 34:
				case 39:
					return ReadString(code);

				case 47:
					return ReadTokenSlash();

				case 37:
				case 42:
					return ReadTokenMultModulo(code);

				case 124:
				case 38:
					return ReadTokenPipeAmp(code);

				case 94:
					return ReadTokenCaret();

				case 43:
				case 45:
					return ReadTokenPlusMin(code);

				case 60:
				case 62:
					return ReadTokenLtGt(code);

				case 61:
				case 33:
					return ReadTokenEqExcl(code);

				case 126:
					return FinishOp(TT["prefix"], 1);
			}

			Raise(State.Position, $"Unexpected character {CodePointToString(code)}");

			return null;
		}

		protected TokenType FinishOp(TokenType type, int size)
		{
			var str = Input.Slice(State.Position, State.Position + size);

			State.Position += size;

			return FinishToken(type, str);
		}

		private TokenType ReadRegexp()
		{
			var start = State.Position;
			var escaped = false;
			var inClass = false;

			for (;;)
			{
				if (State.Position >= Input.Length)
				{
					Raise(start, "Unterminated regular expression");
				}

				var ch = Input[State.Position];

				if (LineBreak.IsMatch(ch.ToString()))
				{
					Raise(start, "Unterminated regular expression");
				}

				if (escaped)
				{
					escaped = false;
				}
				else
				{
					if (ch == '[')
					{
						inClass = true;
					} else if (ch == ']' && inClass)
					{
						inClass = false;
					} else if (ch == '/' && !inClass)
					{
						break;
					}

					escaped = ch == '\\';
				}

				++State.Position;
			}

			var content = Input.Slice(start, State.Position);

			++State.Position;

			var mods = ReadWord1();

			if (!string.IsNullOrEmpty(mods))
			{
				var validFlags = new Regex("^[gmsiyu]*$");

				if (!validFlags.IsMatch(mods))
				{
					Raise(start, "Invalid regular expression flag");
				}
			}

			return FinishToken(TT["regexp"], new Node {Pattern = content, Flags = mods});
		}

		/// <summary>
		/// Read an integer in the given radix. Return null if zero digits
		/// were read, the integer value otherwise. When `len` is given, this
		/// will return `null` unless the integer has exactly `len` digits.
		/// </summary>
		private int? ReadInt(int radix, int? len = null)
		{
			var start = State.Position;
			var total = 0;

			for (int i = 0, e = len ?? int.MaxValue; i < e; ++i)
			{
				var code = Input.CharCodeAt(State.Position);
				int val;

				if (code >= 97)
				{
					val = code - 97 + 10;
				} else if (code >= 65)
				{
					val = code - 65 + 10;
				} else if (code >= 48 && code <= 57)
				{
					val = code - 48;
				}
				else
				{
					val = int.MaxValue;
				}

				if (val >= radix)
				{
					break;
				}

				++State.Position;
				total = total*radix + val;
			}

			if (State.Position == start || len != null && State.Position - start != len)
			{
				return null;
			}

			return total;
		}

		private TokenType ReadRadixNumber(int radix)
		{
			State.Position += 2;

			var val = ReadInt(radix);

			if (val == null)
			{
				Raise(State.Start + 2, "Expected number in radix " + radix);
			}

			if (IsIdentifierStart(FullCharCodeAtPos()))
			{
				Raise(State.Position, "Identifier directly after number");
			}

			return FinishToken(TT["num"], val);
		}

		/// <summary>
		/// Read an integer, octal integer, or floating-point number.
		/// </summary>
		private TokenType ReadNumber(bool startsWithDot)
		{
			var start = State.Position;
			var isFloat = false;
			var octal = Input.CharCodeAt(State.Position) == 48;

			if (!startsWithDot && ReadInt(10) == null)
			{
				Raise(start, "Invalid number");
			}

			int? next = null;

			if (State.Position < Input.Length)
			{
				next = Input.CharCodeAt(State.Position);
			}

			if (next == 46)
			{
				++State.Position;
				ReadInt(10);
				isFloat = true;

				if (State.Position < Input.Length)
				{
					next = Input.CharCodeAt(State.Position);
				}
				else
				{
					next = null;
				}
			}

			if (next == 69 || next == 101)
			{
				next = Input.CharCodeAt(++State.Position);

				if (next == 43 || next == 45)
				{
					++State.Position;
				}

				if (ReadInt(10) == null)
				{
					Raise(start, "Invalid number");
				}

				isFloat = true;
			}

			if (IsIdentifierStart(FullCharCodeAtPos()))
			{
				Raise(State.Position, "Identifier directly after number");
			}

			var str = Input.Slice(start, State.Position);
			object val = null;

			if (isFloat)
			{
				val = float.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
			} else if (!octal || str.Length == 1)
			{
				val = ToInt32(str, 10);
			} else if (new Regex("[89]").IsMatch(str) || State.Strict)
			{
				Raise(start, "Invalid number");
			}
			else
			{
				val = ToInt32(str, 8);
			}

			return FinishToken(TT["num"], val);
		}

		/// <summary>
		/// Read a string value, interpreting backslash-escapes.
		/// </summary>
		private int ReadCodePoint()
		{
			var ch = Input.CharCodeAt(State.Position);
			int code;

			if (ch == 123)
			{
				var codePos = ++State.Position;

				code = ReadHexChar(Input.IndexOf("}", State.Position, StringComparison.Ordinal) - State.Position);
				++State.Position;

				if (code > 0x10FFFF)
				{
					Raise(codePos, "Code point out of bounds");
				}
			}
			else
			{
				code = ReadHexChar(4);
			}

			return code;
		}

		private TokenType ReadString(int quote)
		{
			var outt = "";
			var chunkStart = ++State.Position;

			for (;;)
			{
				if (State.Position >= Input.Length)
				{
					Raise(State.Start, "Unterminated string constant");
				}

				var ch = Input.CharCodeAt(State.Position);

				if (ch == quote)
				{
					break;
				}

				if (ch == 92)
				{
					outt += Input.Slice(chunkStart, State.Position);
					outt += ReadEscapedChar(false);
					chunkStart = State.Position;
				}
				else
				{
					if (IsNewLine(ch))
					{
						Raise(State.Start, "Unterminated string constant");
					}

					++State.Position;
				}
			}

			outt += Input.Slice(chunkStart, State.Position++);

			return FinishToken(TT["string"], outt);
		}

		// Reads template string tokens.
		public TokenType ReadTmplToken()
		{
			var outt = "";
			var chunkStart = State.Position;

			for (;;)
			{
				if (State.Position >= Input.Length)
				{
					Raise(State.Start, "Unterminated template");
				}

				int ch = Input.CharCodeAt(State.Position);

				if (ch == 96 || ch == 36 && Input.CharCodeAt(State.Position + 1) == 123)
				{
					if (State.Position == State.Start && Match(TT["template"]))
					{
						if (ch == 36)
						{
							State.Position += 2;

							return FinishToken(TT["dollarBraceL"]);
						}

						++State.Position;

						return FinishToken(TT["backQuote"]);
					}

					outt += Input.Slice(chunkStart, State.Position);

					return FinishToken(TT["template"], outt);
				}

				if (ch == 92)
				{
					outt += Input.Slice(chunkStart, State.Position);
					outt += ReadEscapedChar(true);
					chunkStart = State.Position;
				} else if (IsNewLine(ch))
				{
					outt += Input.Slice(chunkStart, State.Position);
					++State.Position;

					switch (ch)
					{
						case 10:
						case 13:
							if (ch == 13 && Input.CharCodeAt(State.Position) == 10)
							{
								++State.Position;
							}

							outt += '\n';
							break;

						default:
							outt += ToChar(ch).ToString();
							break;
					}

					++State.CurLine;
					State.LineStart = State.Position;
					chunkStart = State.Position;
				}
				else
				{
					++State.Position;
				}
			}
		}

		/// <summary>
		/// Used to read escaped characters
		/// </summary>
		private string ReadEscapedChar(bool inTemplate )
		{
			int ch = Input.CharCodeAt(++State.Position);

			++State.Position;

			switch (ch)
			{
				case 110:
					return "\n";

				case 114:
					return "\r";

				case 120:
					return ToChar(ReadHexChar(2)).ToString();

				case 117:
					return CodePointToString(ReadCodePoint());

				case 116:
					return "\t";

				case 98:
					return "\b";

				case 118:
					return "\u000b";

				case 102:
					return "\f";

				case 10:
				case 13:
					if (ch == 13 && Input.CharCodeAt(State.Position) == 10)
					{
						++State.Position;
					}

					State.LineStart = State.Position;
					++State.CurLine;
					return "";

				default:
					if (ch >= 48 && ch <= 55)
					{
						var octalStr = new Regex("^[0-7]+").Match(Input.Substr(State.Position - 1, 3)).Value;
						var octal = ToInt32(octalStr, 8);

						if (octal > 255)
						{
							octalStr = octalStr.Slice(0, -1);
							octal = ToInt32(octalStr, 8);
						}

						if (octal > 0)
						{
							if (!State.ContainsOctal)
							{
								State.ContainsOctal = true;
								State.OctalPosition = State.Position - 2;
							}

							if (State.Strict || inTemplate)
							{
								Raise(State.Position - 2, "Octal literal in strict mode");
							}
						}

						State.Position += octalStr.Length - 1;

						return ToChar(octal).ToString();
					}

					return ToChar(ch).ToString();
			}
		}

		/// <summary>
		/// Used to read character escape sequences ('\x', '\u', '\U').
		/// </summary>
		private int ReadHexChar(int len)
		{
			var codePos = State.Position;
			var n = ReadInt(16, len);

			if (n == null)
			{
				Raise(codePos, "Bad character escape sequence");
				return 0;
			}

			return (int) n;
		}

		/// <summary>
		/// Read an identifier, and return it as a string. Sets `this.state.containsEsc`
		/// to whether the word contained a '\u' escape.
		///
		/// Incrementally adds only escaped chars, adding other chunks as-is
		/// as a micro-optimization.
		/// </summary>
		private string ReadWord1()
		{
			State.ContainsEsc = false;

			var word = "";
			var first = true;
			var chunkStart = State.Position;

			while (State.Position < Input.Length)
			{
				var ch = FullCharCodeAtPos();

				if (IsIdentifierChar(ch))
				{
					State.Position += ch <= 0xffff ? 1 : 2;
				} else if (ch == 92)
				{
					State.ContainsEsc = true;

					word += Input.Slice(chunkStart, State.Position);
					var escStart = State.Position;

					if (Input[++State.Position] != 117)
					{
						Raise(State.Position, "Expecting Unicode escape sequence \\uXXXX");
					}

					++State.Position;

					var esc = ReadCodePoint();

					if (!(first ? IsIdentifierStart(esc) : IsIdentifierChar(esc)))
					{
						Raise(escStart, "Invalid Unicode escape");
					}

					word += CodePointToString(esc);
					chunkStart = State.Position;
				}
				else
				{
					break;
				}

				first = false;
			}

			return word + Input.Slice(chunkStart, State.Position);
		}

		/// <summary>
		/// Read an identifier or keyword token. Will check for reserved words when necessary.
		/// </summary>
		/// <returns></returns>
		private TokenType ReadWord()
		{
			var word = ReadWord1();
			var type = TT["name"];

			if (!State.ContainsEsc && IsKeyword(word))
			{
				type = TokenType.Keywords[word];
			}

			return FinishToken(type, word);
		}

		public bool BraceIsBlock(TokenType prevType)
		{
			if (prevType == TT["colon"])
			{
				var parent = CurrentContext;

				if (parent == TokenContext.Types["b_stat"] || parent == TokenContext.Types["b_expr"])
				{
					return !parent.IsExpr;
				}
			}

			if (prevType == TT["_return"])
			{
				return LineBreak.IsMatch(Input.Slice(State.LastTokenEnd, State.Start));
			}

			if (prevType == TT["_else"] || prevType == TT["semi"] ||
				prevType == TT["eof"] || prevType == TT["parenR"])
			{
				return true;
			}

			if (prevType == TT["braceL"])
			{
				return CurrentContext == TokenContext.Types["b_stat"];
			}

			return !State.ExprAllowed;
		}

		protected virtual void UpdateContext(TokenType prevType)
		{
			var type = State.Type;
			var update = type.UpdateContext;

			if (!string.IsNullOrEmpty(type.Keyword) && prevType == TT["dot"])
			{
				State.ExprAllowed = false;
			} else if (update != null)
			{
				update(this, prevType);
			}
			else
			{
				State.ExprAllowed = type.BeforeExpr;
			}
		}

		/// <summary>
		/// Raise an unexpected token error.
		/// </summary>
		protected bool Unexpected(int? pos = null)
		{
			Raise(pos ?? State.Start, "Unexpected token");

			return false;
		}

		/// <summary>
		/// This function is used to raise exceptions on parse errors. It
		/// takes an offset integer (into the current `input`) to indicate
		/// the location of the error, attaches the position to the end
		/// of the error message, and then raises a `SyntaxError` with that
		/// message.
		/// </summary>
		protected void Raise(int pos, string msg)
		{
			var loc = GetLineInfo(Input, pos);

			msg += $" ({loc.Line}:{loc.Column})";

			throw new SyntaxErrorException(msg, loc, pos);
		}

		#endregion
	}
}
	  