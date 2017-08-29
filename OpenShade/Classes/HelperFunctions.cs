using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace OpenShade.Classes

{
    public static class HelperFunctions
    {
        public static string ReplaceAll(this string text, ref bool success, string search, string replace)
        {
            int index = text.IndexOf(search);
            if (index < 0) { success = false; return text; }

            string result = text.Replace(search, replace);
            success = true;
            return result;
        }

        public static string ReplaceFirst(this string text, ref bool success, string search, string replace)
        {
            int index = text.IndexOf(search);
            if (index < 0) { success = false; return text; }

            string result = text.Substring(0, index) + replace + text.Substring(index + search.Length);
            success = true;
            return result;
        }

        public static string ReplaceSecond(this string text, ref bool success, string search, string replace)
        {
            int index = text.IndexOf(search);
            if (index < 0) { success = false; return text; }

            int index2 = text.IndexOf(search, index + search.Length);
            if (index2 < 0) { success = false; return text; }

            string result = text.Substring(0, index2) + replace + text.Substring(index2 + search.Length);
            success = true;
            return result;
        }        

        public static string AddAfter(this string text, ref bool success, string referenceString, string stringToAdd, int skip = 0)
        {
            int index = text.IndexOf(referenceString);
            if (index < 0) { success = false; return text; }

            for (int i = 0; i < skip - 1; i++)
            {
                index = text.IndexOf(referenceString, index + referenceString.Length + 1);
            }

            while (index >= 0)
            {
                text = text.Insert(index + referenceString.Length, stringToAdd);
                index = text.IndexOf(referenceString, index + referenceString.Length + 1);
            }

            success = true;
            return text;
        }

        public static string AddBefore(this string text, ref bool success, string referenceString, string stringToAdd)
        {
            int index = text.IndexOf(referenceString);
            if (index < 0) { success = false; return text; }

            while (index >= 0)
            {
                text = text.Insert(index - 1, stringToAdd);
                index = text.IndexOf(referenceString, index + referenceString.Length + stringToAdd.Length + 1);
            }

            success = true;
            return text;
        }

        public static string CommentOut(this string text, ref bool success, string startingString, string endingString, bool end)
        {
            int index = text.IndexOf(startingString);
            if (index < 0) { success = false; return text; }

            text = text.Insert(index, "/*");

            index = text.IndexOf(endingString);
            if (end) index += endingString.Length; // whether the comment ends at the START of endingString or at its end.
            if (index < 0) { success = false; return text; }

            text = text.Insert(index, "*/\r\n");

            success = true;
            return text;
        }

        public static string CommentOut(this string text, ref bool success, string entireString)
        {
            int index = text.IndexOf(entireString);
            if (index < 0) { success = false; return text; }

            text = text.Insert(index, "/*");

            index += entireString.Length;
            if (index < 0) { success = false; return text; }

            text = text.Insert(index, "*/\r\n");

            success = true;
            return text;
        }


        public static string ToHexString(this string str)
        {
            var sb = new StringBuilder();

            var bytes = Encoding.UTF8.GetBytes(str);
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("X2"));
            }

            return sb.ToString(); // returns: "48656C6C6F20776F726C64" for "Hello world"
        }

        public static string FromHexString(this string hexString)
        {
            var bytes = new byte[hexString.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            string result = Encoding.UTF8.GetString(bytes);
            return result; // returns: "Hello world" for "48656C6C6F20776F726C64"
        }




        public static DependencyObject GetParent(DependencyObject obj)
        {
            if (obj == null)
                return null;

            ContentElement ce = obj as ContentElement;
            if (ce != null)
            {
                DependencyObject parent = ContentOperations.GetParent(ce);
                if (parent != null)
                    return parent;

                FrameworkContentElement fce = ce as FrameworkContentElement;
                return fce != null ? fce.Parent : null;
            }

            FrameworkElement fe = obj as FrameworkElement;
            if (fe != null)
            {
                DependencyObject parent = fe.Parent;
                if (parent != null)
                    return parent;
            }

            return VisualTreeHelper.GetParent(obj);
        }

        /// <summary>
        /// Returns Parent of specified type for the current DependencyObject, or null if no parent of that type exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                T objTest = obj as T;
                if (objTest != null)
                    return objTest;
                obj = GetParent(obj);
            }
            return null;
        }




        public static string GetDictHashCode<T>(List<T> effectList)
        {
            string hash = "";
           
            foreach (var entry in effectList)
            {
                BaseTweak effect = entry as BaseTweak;
                hash += effect.name;
                hash += effect.isEnabled.ToString();
                   
                foreach (var param in effect.parameters)
                {
                    hash += param.name;
                    hash += param.value;
                }                    
            }                      
                        
            return hash;
        }

        public static string GetDictHashCode(ObservableCollection<CustomTweak> effectList)
        {
            string hash = "";
                        
            foreach (var entry in effectList)
            {
                CustomTweak effect = entry as CustomTweak;
                hash += effect.name;
                hash += effect.shaderFile;
                hash += effect.index.ToString();
                hash += effect.oldCode;
                hash += effect.newCode;
                hash += effect.isEnabled.ToString();
            }
           
            return hash;
        }


        public static UIElement FindUid(this DependencyObject parent, string uid)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            if (count == 0) return null;

            for (int i = 0; i < count; i++)
            {
                var el = VisualTreeHelper.GetChild(parent, i) as UIElement;
                if (el == null) continue;

                if (el.Uid == uid) return el;

                el = el.FindUid(uid);
                if (el != null) return el;
            }
            return null;
        }
    }

}
