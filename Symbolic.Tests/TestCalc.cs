// Calcpad Lab — TestCalc: replica EXACTA del patrón Calcpad-Symbolic/TestCalc.cs
// pero envuelve MathParser PRE-PROCESANDO la expresión con MatlabPreprocessor.
//
// Para tests de OPERACIÓN puntual (`2^3`, `[1,2,3]+[4,5,6]`, `eye(3)`, etc.)
// donde queremos verificar VALOR NUMÉRICO final, no HTML.
//
// Si necesitás correr scripts multi-línea con %, ;, for/end, usa TestLab.
using System.Numerics;
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    /// <summary>
    /// Wrapper sobre <see cref="MathParser"/> que aplica preprocessing MATLAB
    /// antes de evaluar la expresión. Permite escribir tests con sintaxis
    /// MATLAB nativa y verificar resultados numéricos.
    /// </summary>
    internal class TestCalc
    {
        private readonly MathParser _parser;

        public TestCalc(MathSettings settings)
        {
            _parser = new MathParser(settings);
        }

        /// <summary>Default: radianes (Degrees=1) para emular MATLAB nativo.</summary>
        public TestCalc() : this(new MathSettings { Degrees = 1 }) { }

        /// <summary>Pasa expresión MATLAB → preprocessor → MathParser. Devuelve <see cref="Real"/>.</summary>
        public double Run(string matlabExpression)
        {
            var expr = MatlabPreprocessor.Process(matlabExpression);
            // El preprocessor agrega un '\n' final; MathParser lo descarta.
            _parser.Parse(expr.TrimEnd('\r', '\n'));
            _parser.Calculate();
            return _parser.Real;
        }

        /// <summary>Corre múltiples expresiones MATLAB en orden (e.g. setup + final).</summary>
        public double Run(string[] matlabExpressions)
        {
            foreach (var e in matlabExpressions)
            {
                var expr = MatlabPreprocessor.Process(e);
                _parser.Parse(expr.TrimEnd('\r', '\n'));
                _parser.Calculate();
            }
            return _parser.Real;
        }

        public double Real => _parser.Real;
        public double Imaginary => _parser.Imaginary;
        public Complex Complex => _parser.Complex;

        public override string ToString() => _parser.ResultAsString;
    }
}
