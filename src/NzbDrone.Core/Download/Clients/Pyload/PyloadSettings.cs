using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Pyload
{
    public class PyloadSettingsValidator : AbstractValidator<PyloadSettings>
    {
        public PyloadSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).GreaterThan(0);

            RuleFor(c => c.Username).NotEmpty()
                                    .WithMessage("Username is required");

            RuleFor(c => c.Password).NotEmpty()
                                    .WithMessage("Password is required");
        }
    }

    public class PyloadSettings : IProviderConfig
    {
        private static readonly PyloadSettingsValidator Validator = new PyloadSettingsValidator();

        public PyloadSettings()
        {
            Host = "localhost";
            Port = 8000;
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "Username", Type = FieldType.Textbox)]
        public string Username { get; set; }

        [FieldDefinition(3, Label = "Password", Type = FieldType.Password)]
        public string Password { get; set; }

        [FieldDefinition(4, Label = "Use SSL", Type = FieldType.Checkbox)]
        public bool UseSsl { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
