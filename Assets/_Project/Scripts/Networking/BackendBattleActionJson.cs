using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Only augments battle-action fields that JsonUtility cannot distinguish (null/missing/zero).
// All other backend response types keep their existing serialization path.
public static class BackendBattleActionJson
{
    public static void RestoreActionPresence(string json, IdleAdventureSessionResponse session)
    {
        if (session == null) return;
        Dictionary<string, object> root = new Reader(json).ReadDocument() as Dictionary<string, object>;
        Dictionary<string, object> data = Field(root, "data") as Dictionary<string, object>;
        List<object> actions = Field(data, "actions") as List<object>;
        if (actions == null || session.actions == null) return;
        if (actions.Count != session.actions.Length) throw new FormatException("Battle action count does not match response.");
        for (int index = 0; index < actions.Count; index++)
        {
            IdleAdventureBattleActionResponse action = session.actions[index];
            Dictionary<string, object> raw = actions[index] as Dictionary<string, object>;
            if (action == null || raw == null) continue;
            action.has_target_health_after = TryLong(raw, "target_health_after", out action.target_health_after);
            object rawTargets = Field(raw, "targets");
            if (rawTargets == null)
            {
                action.targets = null; // Missing/null explicitly keeps the legacy single-target path.
                continue;
            }
            List<object> targets = rawTargets as List<object>;
            if (targets == null) throw new FormatException("Battle targets must be an array or null.");
            action.targets = new IdleAdventureBattleTargetResponse[targets.Count];
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                Dictionary<string, object> value = targets[targetIndex] as Dictionary<string, object>;
                if (value == null) throw new FormatException("Battle target must be an object.");
                IdleAdventureBattleTargetResponse target = new IdleAdventureBattleTargetResponse();
                target.runtime_id = Field(value, "runtime_id") as string;
                target.target_name = Field(value, "target_name") as string;
                target.defender_attribute = Field(value, "defender_attribute") as string;
                if (TryLong(value, "slot_index", out long slot)) target.slot_index = checked((int)slot);
                TryLong(value, "health_before", out target.health_before);
                target.has_health_after = TryLong(value, "health_after", out target.health_after);
                TryLong(value, "damage", out target.damage);
                TryLong(value, "healed_amount", out target.healed_amount);
                object defeated = Field(value, "defeated");
                if (defeated != null && !(defeated is bool)) throw new FormatException("Battle defeated must be a boolean or null.");
                target.has_defeated = defeated is bool;
                target.defeated = defeated is bool flag && flag;
                action.targets[targetIndex] = target;
            }
        }
    }

    private static object Field(Dictionary<string, object> value, string key)
    {
        return value != null && value.TryGetValue(key, out object result) ? result : null;
    }

    private static bool TryLong(Dictionary<string, object> value, string key, out long result)
    {
        result = 0;
        object raw = Field(value, key);
        if (raw == null) return false;
        if (!(raw is Number number) || !long.TryParse(number.text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
            throw new FormatException("Invalid integer in battle field: " + key);
        return true;
    }

    private sealed class Number
    {
        public readonly string text;
        public Number(string value) { text = value; }
    }

    // Small strict JSON reader: numbers remain strings so 64-bit HP values never lose precision.
    private sealed class Reader
    {
        private readonly string text;
        private int cursor;
        private int depth;
        public Reader(string json) { text = json ?? throw new ArgumentNullException(nameof(json)); }

        public object ReadDocument()
        {
            object result = ReadValue();
            SkipSpace();
            if (cursor != text.Length) throw Error();
            return result;
        }

        private object ReadValue()
        {
            SkipSpace();
            if (cursor >= text.Length || ++depth > 128) throw Error();
            try
            {
                char token = text[cursor];
                if (token == '{') return ReadObject();
                if (token == '[') return ReadArray();
                if (token == '"') return ReadString();
                if (token == 't') { Literal("true"); return true; }
                if (token == 'f') { Literal("false"); return false; }
                if (token == 'n') { Literal("null"); return null; }
                return ReadNumber();
            }
            finally { depth--; }
        }

        private Dictionary<string, object> ReadObject()
        {
            cursor++;
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (Take('}')) return result;
            do
            {
                SkipSpace();
                string key = ReadString();
                if (!Take(':')) throw Error();
                if (result.ContainsKey(key)) throw new FormatException("Duplicate JSON field: " + key);
                result.Add(key, ReadValue());
                if (Take('}')) return result;
            } while (Take(','));
            throw Error();
        }

        private List<object> ReadArray()
        {
            cursor++;
            List<object> result = new List<object>();
            if (Take(']')) return result;
            do
            {
                result.Add(ReadValue());
                if (Take(']')) return result;
            } while (Take(','));
            throw Error();
        }

        private string ReadString()
        {
            if (cursor >= text.Length || text[cursor++] != '"') throw Error();
            StringBuilder result = new StringBuilder();
            while (cursor < text.Length)
            {
                char value = text[cursor++];
                if (value == '"') return result.ToString();
                if (value < 32) throw Error();
                if (value != '\\') { result.Append(value); continue; }
                if (cursor >= text.Length) throw Error();
                switch (text[cursor++])
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (cursor + 4 > text.Length || !ushort.TryParse(text.Substring(cursor, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code)) throw Error();
                        result.Append((char)code);
                        cursor += 4;
                        break;
                    default: throw Error();
                }
            }
            throw Error();
        }

        private Number ReadNumber()
        {
            int start = cursor;
            if (cursor < text.Length && text[cursor] == '-') cursor++;
            if (cursor >= text.Length) throw Error();
            if (text[cursor] == '0') cursor++;
            else
            {
                if (text[cursor] < '1' || text[cursor] > '9') throw Error();
                Digits();
            }
            if (cursor < text.Length && text[cursor] == '.') { cursor++; RequiredDigits(); }
            if (cursor < text.Length && (text[cursor] == 'e' || text[cursor] == 'E'))
            {
                cursor++;
                if (cursor < text.Length && (text[cursor] == '+' || text[cursor] == '-')) cursor++;
                RequiredDigits();
            }
            return new Number(text.Substring(start, cursor - start));
        }

        private void Digits() { while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9') cursor++; }
        private void RequiredDigits() { int start = cursor; Digits(); if (cursor == start) throw Error(); }
        private void Literal(string value)
        {
            if (cursor + value.Length > text.Length || string.CompareOrdinal(text, cursor, value, 0, value.Length) != 0) throw Error();
            cursor += value.Length;
        }
        private bool Take(char token)
        {
            SkipSpace();
            if (cursor >= text.Length || text[cursor] != token) return false;
            cursor++;
            return true;
        }
        private void SkipSpace() { while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == '\r' || text[cursor] == '\n' || text[cursor] == '\t')) cursor++; }
        private FormatException Error() { return new FormatException("Invalid battle response JSON at offset " + cursor); }
    }
}
