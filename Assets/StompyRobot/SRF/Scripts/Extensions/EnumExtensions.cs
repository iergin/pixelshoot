using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public static class EnumExtensions 
{
    public static string ToDescriptionString(this UrlVersions val)
    {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val
           .GetType()
           .GetField(val.ToString())
           .GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : string.Empty;
    }
}
public enum UrlVersions
{
    [Description("https://gsfrqa.fusegames.io")]
    QA = 1,
    [Description("https://tdsgs.fusegames.io")]
    Prod = 2,
}