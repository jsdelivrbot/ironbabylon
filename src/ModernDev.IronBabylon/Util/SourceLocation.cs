// Copyright (c) Bohdan Shtepan. All rights reserved.
// http://modern-dev.com/
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ModernDev.IronBabylon
{
    public class SourceLocation
    {
        public SourceLocation(Position start = null, Position end = null)
        {
            Start = start;
            End = end;
        }

        public Position Start { get; set; }
        public Position End { get; set; }
    }
}