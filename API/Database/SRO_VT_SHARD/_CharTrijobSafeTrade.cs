﻿using System;
using System.Collections.Generic;

namespace API.Database.SRO_VT_SHARD;

public partial class _CharTrijobSafeTrade
{
    public int CharID { get; set; }

    public int AbleCount { get; set; }

    public int Status { get; set; }

    public DateTime LastSafeTrade { get; set; }
}
