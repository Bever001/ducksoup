﻿using System;
using System.Collections.Generic;

namespace API.Database.SRO_VT_SHARD;

public partial class _RefSiegeFortressReward
{
    public byte Service { get; set; }

    public int FortressID { get; set; }

    public byte RewardTypeID { get; set; }

    public int RewardValue { get; set; }

    public byte RewardCount { get; set; }
}
