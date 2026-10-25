using System.Globalization;

namespace StrategyTradingLab.Api.Auth;

/// <summary>A message for the user in both languages. <c>{0}</c> placeholders take the arguments of <see cref="In"/>.</summary>
public readonly record struct Message(string En, string Ca)
{
    /// <summary>The message in <paramref name="locale"/> ("ca", else English), numbers with a dot whatever the server's culture.</summary>
    public string In(string locale, params object[] args)
    {
        var text = locale == "ca" ? Ca : En;
        return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
    }
}

/// <summary>
/// Every message the API shows a user (validation, wrong log-in, links, signals, strategies), English and Catalan, by name
/// (glossary: docs/glossary-ca.md). Admin messages are not here: the owner reads those in English.
/// </summary>
public static class Messages
{
    // Account
    public static readonly Message UseAnEmail = new("Use an email address, e.g. you@gmail.com.", "Fes servir una adreça de correu, p. ex. tu@gmail.com.");
    public static readonly Message PasswordTooShort = new("Use at least {0} characters.", "Fes servir almenys {0} caràcters.");
    public static readonly Message PasswordTooLong = new("Use at most {0} characters.", "Fes servir com a màxim {0} caràcters.");
    public static readonly Message PasswordIsEmail = new("Don't use your email as the password.", "No facis servir el teu correu com a contrasenya.");
    public static readonly Message PickLanguage = new("Pick English or Catalan.", "Tria anglès o català.");
    public static readonly Message PickCurrency = new("Pick a currency from the list.", "Tria una moneda de la llista.");
    public static readonly Message PickTimeZone = new("Pick your time zone.", "Tria la teva zona horària.");
    public static readonly Message TickTheBox = new("Tick the box to go on.", "Marca la casella per continuar.");
    public static readonly Message CheckYourEmail = new("Check your email.", "Revisa el teu correu.");
    public static readonly Message WrongLogIn = new("That email and password don't match.", "Aquest correu i aquesta contrasenya no coincideixen.");
    public static readonly Message TooManyTries = new("Too many tries. Wait a few minutes, or reset your password.",
        "Massa intents. Espera uns minuts o restableix la contrasenya.");
    public static readonly Message ExpiredLink = new("This link has expired. Ask for a new one.", "Aquest enllaç ha caducat. Demana'n un de nou.");
    public static readonly Message SayTrueOrFalse = new("Say true or false.", "Indica si està activat o desactivat.");
    public static readonly Message ResetLinkSent = new("If that email has an account, we sent a link. It works for 1 hour.",
        "Si aquest correu té un compte, t'hem enviat un enllaç. Funciona durant 1 hora.");
    public static readonly Message WrongCurrentPassword = new("That isn't your current password.", "Aquesta no és la teva contrasenya actual.");

    // Signals
    public static readonly Message AlreadyAnswered = new("This signal is already answered.", "Aquest senyal ja té resposta.");
    public static readonly Message OnlyWaitingCanSnooze = new("Only a signal still waiting can be snoozed.", "Només es pot ajornar un senyal que encara espera.");
    public static readonly Message StrategyEnded = new("This strategy has ended, so there is no reminder to send.",
        "Aquesta estratègia ha acabat, així que no hi ha cap recordatori per enviar.");
    public static readonly Message NotificationsOff = new("This strategy's notifications are off, so there is no reminder to send.",
        "Les notificacions d'aquesta estratègia estan desactivades, així que no hi ha cap recordatori per enviar.");
    public static readonly Message ConfirmEmailForReminders = new("Confirm your email address to get reminders.", "Confirma la teva adreça de correu per rebre recordatoris.");

    public static readonly Message LiveOnlyLog = new("Trades can only be logged for live strategies.", "Només es poden registrar operacions en estratègies en directe.");
    public static readonly Message LiveOnlyEdit = new("Only live trades can be edited.", "Només es poden editar les operacions en directe.");
    public static readonly Message LiveOnlyDelete = new("Only live trades can be deleted.", "Només es poden eliminar les operacions en directe.");
    public static readonly Message PickStrategyStock = new("Pick one of the strategy's stocks.", "Tria una de les accions de l'estratègia.");

    public static readonly Message EnterTradeDay = new("Enter the day you traded.", "Indica el dia que vas operar.");
    public static readonly Message DayAfterSignal = new("Use a day on or after the signal.", "Tria un dia igual o posterior al senyal.");
    public static readonly Message DayUpToToday = new("Use a day up to today.", "Tria un dia fins a avui.");
    public static readonly Message EnterPrice = new("Enter the price per share.", "Indica el preu per acció.");
    public static readonly Message UpToDecimals = new("Use up to {0} decimals.", "Fes servir com a màxim {0} decimals.");
    public static readonly Message EnterAmount = new("Enter the amount you spent, without the fee.", "Indica l'import que has gastat, sense la comissió.");
    public static readonly Message EnterShares = new("Enter the number of shares.", "Indica el nombre d'accions.");
    public static readonly Message EnterFee = new("Enter the fee, or leave it empty.", "Indica la comissió, o deixa-ho buit.");
    public static readonly Message EnterBrokerRate = new("Enter the rate your broker used.", "Indica el tipus de canvi que ha fet servir el teu broker.");
    public static readonly Message NoReferenceRate = new("There is no reference rate for that day yet: enter your broker's.",
        "Encara no hi ha cap tipus de canvi de referència per a aquest dia: indica el del teu broker.");
    public static readonly Message TradeTooSmall = new("That is too small to be a trade.", "Això és massa petit per ser una operació.");
    public static readonly Message TradeTooBig = new("That is more than one trade can be.", "Això és més del que pot ser una operació.");

    // Strategies
    public static readonly Message NameTaken = new("You already have a running strategy with that name.", "Ja tens una estratègia en marxa amb aquest nom.");
    public static readonly Message UseNameLength = new("Use 1 to {0} characters.", "Fes servir d'1 a {0} caràcters.");
    public static readonly Message PickMode = new("Pick simulation or live.", "Tria simulació o en directe.");
    public static readonly Message ListedOnce = new("Each stock or fund can be listed once.", "Cada acció o fons es pot afegir només un cop.");
    public static readonly Message PickTheFund = new("Pick the fund.", "Tria el fons.");
    public static readonly Message PickOneStock = new("Pick at least one stock.", "Tria almenys una acció.");
    public static readonly Message PickExactlyOneFund = new("Pick exactly one fund.", "Tria exactament un fons.");
    public static readonly Message NotAFund = new("That is not a fund we can follow.", "Aquest no és un fons que puguem seguir.");
    public static readonly Message NotStocks = new("Some of those are not stocks we can follow.", "Algunes d'aquestes no són accions que puguem seguir.");
    public static readonly Message SetBudget = new("Set a budget.", "Fixa un pressupost.");
    public static readonly Message WholeBudget = new("Use a whole amount from {0:0} to {1:0}.", "Fes servir un import sencer entre {0:0} i {1:0}.");
    public static readonly Message NoSteps = new("This template has no steps.", "Aquesta plantilla no té passos.");
    public static readonly Message WholeStep = new("Use a whole amount from {0:0} up to the budget.", "Fes servir un import sencer des de {0:0} fins al pressupost.");
    public static readonly Message UseSteps = new("Use 1 to {0} steps.", "Fes servir d'1 a {0} passos.");

    // Advanced settings
    public static readonly Message NoSuchSetting = new("This template has no such setting.", "Aquesta plantilla no té aquest ajust.");
    public static readonly Message SettingRange = new("Use {0:0.##} to {1:0.##}, in steps of {2:0.##}.", "Fes servir de {0:0.##} a {1:0.##}, en passos de {2:0.##}.");
    public static readonly Message SettingBelow = new("Keep it at least {0:0.##} under the setting after it.", "Mantén-ho com a mínim {0:0.##} per sota de l'ajust següent.");

    // AI rounds (Your AI's top picks)
    public static readonly Message PasteAnswer = new("Paste your AI's answer.", "Enganxa la resposta de la teva IA.");
    public static readonly Message AnswerTooLong = new("That answer is too long to read. Ask your AI for the table only.",
        "Aquesta resposta és massa llarga per llegir-la. Demana a la teva IA només la taula.");
    public static readonly Message NoScoresTable = new("We couldn't find the scores table. Ask your AI to answer only with the table.",
        "No hem trobat la taula de puntuacions. Demana a la teva IA que respongui només amb la taula.");
    public static readonly Message NotYourStock = new("\"{0}\" isn't one of your stocks.", "«{0}» no és cap de les teves accions.");
    public static readonly Message ScoredTwice = new("{0} is scored more than once.", "{0} està puntuada més d'una vegada.");
    public static readonly Message BadScore = new("{0}: the overall score isn't a number from 0 to 100.", "{0}: la puntuació global no és un número entre 0 i 100.");
    public static readonly Message NoScore = new("{0} has no score.", "{0} no té puntuació.");
    public static readonly Message MoreProblems = new("And {0} more.", "I {0} més.");
    public static readonly Message PickAiModel = new("Pick the AI model you used, or name it.", "Tria el model d'IA que has fet servir, o escriu-ne el nom.");
    public static readonly Message RoundNotDue = new("This AI round isn't due yet.", "Encara no és l'hora d'aquesta ronda d'IA.");
    public static readonly Message RoundDone = new("This AI round is already done.", "Aquesta ronda d'IA ja està feta.");
    public static readonly Message OneSourceOn = new("At least one source stays on.", "Almenys una font queda activada.");
    public static readonly Message InstructionsLength = new("Keep it under {0} characters.", "Que no passi de {0} caràcters.");
    public static readonly Message InstructionsEmpty = new("Write the instructions, or reset to the suggested ones.",
        "Escriu les instruccions, o torna a les suggerides.");
}

