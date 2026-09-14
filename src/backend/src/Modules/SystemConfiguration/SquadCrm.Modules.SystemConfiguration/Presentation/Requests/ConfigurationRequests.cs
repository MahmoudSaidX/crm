using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.SystemConfiguration.Presentation.Requests;

public sealed record UpdateConfigurationValueRequest([property: Required, MaxLength(2000)] string Value);
