﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Database.DuckSoup;

[Table("User")]
public class User
{
    public Guid userId { get; set; } = Guid.NewGuid();

    public string username { get; set; }

    public byte[] passwordHash { get; set; }

    public byte[] passwordSalt { get; set; }

    public int tokenVersion { get; set; } = 0;
}