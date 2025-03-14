﻿using System;
using UnityEngine;

namespace LocalizationSystem
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
    public sealed class LocalizationId : PropertyAttribute
    {
        public readonly string Table;

        public LocalizationId(string table)
        {
            Table = table;
        }
    }
}