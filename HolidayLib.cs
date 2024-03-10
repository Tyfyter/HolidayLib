using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace HolidayLib {
	public class HolidayLib : Mod {
		List<Holiday> holidays;
		MethodInfo _IsActive;
		internal int holidayVersion;
		public static class Months {
			public const int January   = 1;
			public const int February  = 2;
			public const int March     = 3;
			public const int April     = 4;
			public const int May       = 5;
			public const int June      = 6;
			public const int July      = 7;
			public const int August    = 8;
			public const int September = 9;
			public const int October   = 10;
			public const int November  = 11;
			public const int December  = 12;
		}
		public HolidayLib() {
			holidays = new();
			_IsActive = typeof(Holiday).GetProperty("IsActive").GetGetMethod();
			AddHoliday("April fools", new DateRange((Months.April, 1)));
			AddAliases("April fools", "April fool's", "April fools'", "April fools day", "April fool's day", "April fools' day");
			AddHoliday("Saint Patrick's Day", new DateRange((Months.March, 5), (Months.March, 31)));
			AddAliases("Saint Patrick's Day", "Saint Patricks Day", "St. Patrick's Day", "St Patricks Day", "Feast of Saint Patrick");
			AddHoliday("Oktoberfest", new DateRange((Months.September, 27), (Months.October, 31)));

			AddHoliday("Spring Equinox", new DateRange((Months.March, 19), (Months.March, 21)));
			AddAliases("Spring Equinox", "Vernal Equinox", "March Equinox", "Northward Equinox");

			AddHoliday("Summer Solstice", new DateRange((Months.June, 20), (Months.June, 22)));
			AddAliases("Summer Solstice", "Estival Solstice", "June Solstice");

			AddHoliday("Autumnal Equinox", new DateRange((Months.September, 21), (Months.September, 24)));
			AddAliases("Autumnal Equinox", "Fall Equinox", "September Equinox", "Southward Equinox");

			AddHoliday("Winter Solstice", new DateRange((Months.December, 20), (Months.December, 22)));
			AddAliases("Winter Solstice", "Hibernal Solstice", "December Solstice");
		}
		public override void Load() {
			On_Main.checkXMas += (orig) => {
				bool wasXMas = Main.xMas;
				orig();
				if (wasXMas != Main.xMas) holidayVersion++;
			};
			On_Main.checkHalloween += (orig) => {
				bool wasHalloween = Main.halloween;
				orig();
				if (wasHalloween != Main.halloween) holidayVersion++;
			};
		}
		public override object Call(params object[] args) {
			string command = FormatForComparison(args[0].ToString());
			bool callFunc = true;
			if (command == "GETFUNC") {
				callFunc = false;
				command = FormatForComparison(args[1].ToString());
			}
			Func<object[], object> func;
			switch (command) {
				case "ADDHOLIDAY":
				func = (object[] args) => {
					string name = args[0] as string;
					if (args[1] is DateTime start) {
						if (args.Length < 3) {
							return AddHoliday(name, new DateRange((start.Month, start.Day)));
						} else if (args[2] is DateTime end) {
							return AddHoliday(name, new DateRange((start.Month, start.Day), (end.Month, end.Day)));
						} else if (args[2] is int duration) {
							return AddHoliday(name, new DateRange((start.Month, start.Day), duration));
						}
					}
					if (args[1] is DateRange) {
						return AddHoliday(name, args.Skip(1).Where(v => v is DateRange).Select(v => (DateRange)v).ToArray());
					}
					if (args[1] is Func<int>) {
						return AddHoliday(name, args.Skip(1).Select(v => v as Func<int>).Where(v => v is not null).ToArray());
					}
					return new ArgumentException();
				};
				break;

				case "ADDALIAS" or "ADDALIASES":
				func = (object[] args) => {
					string name = args[0] as string;
					AddAliases(name, args.Skip(1).Select(v => v as string).Where(v => v is not null).ToArray());
					return null;
				};
				break;

				case "ISACTIVE":
				func = (object[] args) => {
					string name = args[0] as string;
					return GetHoliday(name).IsActive;
				};
				break;

				case "GETACTIVELOOKUP":
				func = (object[] args) => {
					string name = args[0] as string;
					Holiday holiday = GetHoliday(name);
					return holiday.isActiveLookup ??= _IsActive.CreateDelegate<Func<bool>>(holiday);
				};
				break;

				case "HOLIDAYFORCECHANGED":
				func = (object[] args) => {
					unchecked {
						return ++holidayVersion;
					}
				};
				break;

				case "FORCEDHOLIDAYVERSION":
				func = (object[] args) => holidayVersion;
				break;

				default:
				case "HELP":
				func = (object[] args) => "See description for documentation";
				break;
			}
			return callFunc ? func(args.Skip(1).ToArray()) : func;
		}
		static string FormatForComparison(string str) => str.ToUpperInvariant().Replace(" ", "").Replace("_", "");
		public Holiday AddHoliday(string name, params DateRange[] dateRanges) {
			return AddHoliday(name, new Holiday(dateRanges));
		}
		public Holiday AddHoliday(string name, params Func<int>[] conditions) {
			return AddHoliday(name, new Holiday(conditions));
		}
		public Holiday AddHoliday(string name, Holiday holiday) {
			name = FormatForComparison(name);
			holiday.Names.Add(name);
			if (TryGetHoliday(name, out Holiday other)) {
				other.MergeWith(holiday);
			} else {
				holidays.Add(holiday);
			}
			return holiday;
		}
		public Holiday GetHoliday(string name) {
			name = FormatForComparison(name);
			if (!TryGetHoliday(name, out Holiday holiday)) {
				return null;
			}
			return holiday;
		}
		public void AddAliases(string name, params string[] aliases) {
			if (TryGetHoliday(FormatForComparison(name), out var holiday)) {
				foreach (string alias in aliases) holiday.Names.Add(FormatForComparison(alias));
			} else {
				throw new ArgumentException($"No such holiday \"{name}\" has been added", nameof(name));
			}
		}
		bool TryGetHoliday(string name, out Holiday holiday) {
			for (int i = 0; i < holidays.Count; i++) {
				Holiday _holiday = holidays[i];
				if (_holiday.Names.Contains(name)) {
					holiday = _holiday;
					return true;
				}
			}
			holiday = null;
			return false;
		}
	}
	public class HolidaySystem : ModSystem {
		int lastDay = -1;
		public override void PostUpdateWorld() {
			if (DateTime.Now.Day != lastDay) {
				lastDay = DateTime.Now.Day;
				ModContent.GetInstance<HolidayLib>().holidayVersion++;
			}
		}
	}
	public class CheckHolidayCommand : ModCommand {
		public override string Command => "checkholiday";
		public override CommandType Type => CommandType.Chat;
		public override string Description => "checks if a holiday is active";
		public override string Usage => "/checkholiday <holiday name>";
		public override void Action(CommandCaller caller, string input, string[] args) {
			input = input.Substring(Command.Length + 2);
			if (ModContent.GetInstance<HolidayLib>().GetHoliday(input) is Holiday holiday) {
				caller.Reply(input + (holiday.IsActive ? " is active" : " is not active"));
			} else {
				caller.Reply($"No such holiday \"{input}\" has been added", Color.Firebrick);
			}
		}
	}
	public class CallCommand : ModCommand {
		public override string Command => "hl-call";
		public override CommandType Type => CommandType.Chat;
		public override void Action(CommandCaller caller, string input, string[] args) {
			caller.Reply(ModContent.GetInstance<HolidayLib>().Call(args)?.ToString() ?? "null");
		}
	}
	public class Holiday {
		public List<DateRange> DateRanges { get; private set; }
		public List<Func<int>> Conditions { get; private set; }
		HashSet<string> names;
		public HashSet<string> Names => names ??= new();
		internal Func<bool> isActiveLookup;
		public Holiday() { }
		public Holiday(params DateRange[] dateRanges) {
			DateRanges = new(dateRanges);
		}
		public Holiday(params Func<int>[] conditions) {
			Conditions = new(conditions);
		}
		public bool IsActive {
			get {
				DateTime today = DateTime.Today;
				int sway = 0;
				DateRanges ??= new();
				Conditions ??= new();
				for (int i = 0; i < Conditions.Count; i++) {
					sway += Conditions[i]();
				}
				if (sway != 0) return sway > 0;
				for (int i = 0; i < DateRanges.Count; i++) {
					if (DateRanges[i].Contains(today)) {
						return true;
					}
				}
				return false;
			}
		}
		public void MergeWith(Holiday other) {
			DateRanges ??= new();
			other.DateRanges ??= new();
			for (int i = 0; i < other.DateRanges.Count; i++) {
				if (!DateRanges.Contains(other.DateRanges[i])) {
					DateRanges.Add(other.DateRanges[i]);
				}
			}
			Conditions ??= new();
			other.Conditions ??= new();
			Conditions = Conditions.Union(other.Conditions).ToList();
		}
	}
	public struct DateRange {
		public readonly (int month, int day) start, end;
		public DateRange((int month, int day) start, (int month, int day) end) {
			(this.start, this.end) = (start, end);
		}
		public DateRange((int month, int day) start, int durationDays = 1) {
			this.start = start;
			DateTime end = new DateTime(DateTime.Now.Year, start.month, start.day).AddDays(durationDays);
			this.end = (end.Month, end.Day);
		}
		public bool Contains(DateTime date) {
			(int month, int day) value = (date.Month, date.Day);
			switch (Compare(end, start)) {
				case 1:
				return Compare(value, start) >= 0 && Compare(value, end) <= 0;

				default:
				case 0:
				return Compare(value, start) == 0 && Compare(value, end) == 0;

				case -1:
				return Compare(value, start) >= 0 || Compare(value, end) <= 0;
			}
		}
		/// <summary>
		/// compares two yearless dates
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns>if a is after b: 1, if a is before b: -1, of a and b are equal: 0</returns>
		static int Compare((int month, int day) a, (int month, int day) b) {
			if (a.month == b.month && a.day == b.day) return 0;
			if (a.month > b.month || (a.month == b.month && a.day > b.day)) return 1;
			return -1;
		}
		public override bool Equals([NotNullWhen(true)] object obj) {
			if (obj is not DateRange other) return false;
			return Compare(start, other.start) == 0 && Compare(end, other.end) == 0;
		}
		public override int GetHashCode() {
			return HashCode.Combine(start.month, start.day, start.month, end.day);
		}
		public static bool operator ==(DateRange left, DateRange right) {
			return left.Equals(right);
		}
		public static bool operator !=(DateRange left, DateRange right) {
			return !(left == right);
		}
	}
}