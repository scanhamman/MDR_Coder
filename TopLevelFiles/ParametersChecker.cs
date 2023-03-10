using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace MDR_Coder;

internal class ParameterChecker 
{
    private readonly ILoggingHelper _loggingHelper;
    private readonly IMonDataLayer _monDataLayer;
    private readonly ITestingDataLayer _testRepo;

    public ParameterChecker(IMonDataLayer monDataLayer, ITestingDataLayer testRepo, 
        ILoggingHelper loggingHelper)
    {
        _monDataLayer = monDataLayer;
        _testRepo = testRepo;
        _loggingHelper = loggingHelper;
    }
    
    public ParamsCheckResult CheckParams(string[]? args)
    {
        // Calls the CommandLine parser. If an error in the initial parsing, log it 
        // and return an error. If parameters can be passed, check their validity
        // and if invalid log the issue and return an error, otherwise return the 
        // parameters, processed as an instance of the Options class, and the source.

        var parsedArguments = Parser.Default.ParseArguments<Options>(args);
        if (parsedArguments.Errors.Any())
        {
            LogParseError(((NotParsed<Options>)parsedArguments).Errors);
            return new ParamsCheckResult(true, false, null);
        }
        else
        {
            var opts = parsedArguments.Value;
            return CheckArgumentValuesAreValid(opts);
        }
    }
    
   
    public ParamsCheckResult CheckArgumentValuesAreValid(Options opts)
    {
        // 'opts' is passed by reference and may be changed by the checking mechanism.

        try
        {
            if (opts.UsingTestData is true)
            {
                // Set up array of source ids to reflect
                // those in the test data set.

                opts.SourceIds = _testRepo.ObtainTestSourceIDs();
                return new ParamsCheckResult(false, false, opts);;     // Should always be able to run
            }
            else if (opts.CreateTestReport is true)
            {
                return new ParamsCheckResult(false, false, opts);;     // Should always be able to run
            }
            else
            { 
                if (opts.SourceIds?.Any() is not true)
                {
                    throw new ArgumentException("No source id provided");
                }

                foreach (int sourceId in opts.SourceIds)
                {
                    if (!_monDataLayer.SourceIdPresent(sourceId))
                    {
                        throw new ArgumentException("Source argument " + sourceId.ToString() +
                                                    " does not correspond to a known source");
                    }
                }
                
                // parameters valid - return opts.

                return new ParamsCheckResult(false, false, opts);
            }
        }

        catch (Exception e)
        {
            _loggingHelper.OpenNoSourceLogFile();
            _loggingHelper.LogHeader("INVALID PARAMETERS");
            _loggingHelper.LogCommandLineParameters(opts);
            _loggingHelper.LogCodeError("Importer application aborted", e.Message, e.StackTrace);
            _loggingHelper.CloseLog();
            return new ParamsCheckResult(false, true, null);;
        }

    }


    internal void LogParseError(IEnumerable<Error> errs)
    {
        _loggingHelper.OpenNoSourceLogFile();
        _loggingHelper.LogHeader("UNABLE TO PARSE PARAMETERS");
        _loggingHelper.LogHeader("Error in input parameters");
        _loggingHelper.LogLine("Error in the command line arguments - they could not be parsed");

        int n = 0;
        foreach (Error e in errs)
        {
            n++;
            _loggingHelper.LogParseError("Error {n}: Tag was {Tag}", n.ToString(), e.Tag.ToString());
            if (e.GetType().Name == "UnknownOptionError")
            {
                _loggingHelper.LogParseError("Error {n}: Unknown option was {UnknownOption}", n.ToString(), ((UnknownOptionError)e).Token);
            }
            if (e.GetType().Name == "MissingRequiredOptionError")
            {
                _loggingHelper.LogParseError("Error {n}: Missing option was {MissingOption}", n.ToString(), ((MissingRequiredOptionError)e).NameInfo.NameText);
            }
            if (e.GetType().Name == "BadFormatConversionError")
            {
                _loggingHelper.LogParseError("Error {n}: Wrongly formatted option was {MissingOption}", n.ToString(), ((BadFormatConversionError)e).NameInfo.NameText);
            }
        }
        _loggingHelper.LogLine("MDR_Downloader application aborted");
        _loggingHelper.CloseLog();
    }

}

public class Options
{
    // Lists the command line arguments and options

    [Option('s', "source_ids", Required = false, Separator = ',', HelpText = "Comma separated list of Integer ids of data sources.")]
    public IEnumerable<int>? SourceIds { get; set; }

    [Option('T', "build tables", Required = false, HelpText = "If present, forces the (re)creation of a new set of ad tables")]
    public bool? RebuildAdTables { get; set; }

    [Option('F', "is a test", Required = false, HelpText = "If present, operates on the sd / ad tables in the test database")]
    public bool? UsingTestData { get; set; }

    [Option('G', "test report", Required = false, HelpText = "If present, compares and reports on adcomp and expected tables but does not recreate those tables")]
    public bool? CreateTestReport { get; set; }
}


public class ParamsCheckResult
{
    internal bool ParseError { get; set; }
    internal bool ValidityError { get; set; }
    internal Options? Pars { get; set; }

    internal ParamsCheckResult(bool parseError, bool validityError, Options? pars)
    {
        ParseError = parseError;
        ValidityError = validityError;
        Pars = pars;
    }
}
