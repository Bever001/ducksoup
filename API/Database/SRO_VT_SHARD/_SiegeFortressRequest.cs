﻿using System;
using System.Collections.Generic;

namespace API.Database.SRO_VT_SHARD;

public partial class _SiegeFortressRequest
{
    public int FortressID { get; set; }

    public int GuildID { get; set; }

    public byte RequestType { get; set; }
}
