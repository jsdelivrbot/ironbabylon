// Copyright (c) Bohdan Shtepan. All rights reserved.
// http://modern-dev.com/
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ModernDev.IronBabylon
{
    public class Position
    {
        public Position(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int? Line { get; private set; }
        public int? Column { get; private set; }
    }
}