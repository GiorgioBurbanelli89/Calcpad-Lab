using System;

namespace Calcpad.Core
{
    /// <summary>
    /// Genera código MATLAB PORTABLE (2017a) para operaciones de acumulación
    /// escritas en notación matemática: sumatoria (Σ), productoria (∏) e
    /// integral (∫). El usuario elige la FORMA: por bucle `for` explícito (se ve
    /// cada iteración) o por función builtin (compacto). Para la integral hay
    /// varias formas disponibles. Alimenta a la ventana-loop de Hekatan Lab.
    /// Todo lo que emite corre igual en MATLAB real.
    /// </summary>
    public static class LoopBuilder
    {
        public enum Op { Sum, Product, Integral }

        /// <summary>Formas disponibles. Suma/Producto: Loop | Function.
        /// Integral: Function(integral) | FunctionTrapz | LoopTrapezoid | LoopSimpson.</summary>
        public enum Form { Loop, Function, FunctionTrapz, LoopTrapezoid, LoopSimpson }

        /// <summary>Devuelve el MATLAB para (op, expr, var, from, to, form). `result`
        /// es el nombre de la variable resultado. Para las formas "Function" es una
        /// sola expresión asignada; para las "Loop" es un bloque con for/end.</summary>
        public static string Build(Op op, string expr, string var, string from, string to, Form form, string result = null)
        {
            expr = (expr ?? "").Trim();
            var = string.IsNullOrWhiteSpace(var) ? "k" : var.Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();
            string res = string.IsNullOrWhiteSpace(result)
                ? (op == Op.Sum ? "s" : op == Op.Product ? "p" : "I")
                : result.Trim();
            string nl = "\n";

            switch (op)
            {
                case Op.Sum:
                    return form == Form.Function
                        ? $"{res} = sum(arrayfun(@({var}) ({expr}), {from}:{to}));"
                        : $"{res} = 0;{nl}for {var} = {from}:{to}{nl}    {res} = {res} + ({expr});{nl}end";

                case Op.Product:
                    return form == Form.Function
                        ? $"{res} = prod(arrayfun(@({var}) ({expr}), {from}:{to}));"
                        : $"{res} = 1;{nl}for {var} = {from}:{to}{nl}    {res} = {res} * ({expr});{nl}end";

                case Op.Integral:
                    switch (form)
                    {
                        case Form.Function: // integral() adaptivo (matriz-seguro con 'ArrayValued')
                            return $"{res} = integral(@({var}) ({expr}), {from}, {to});";
                        case Form.FunctionTrapz: // trapz sobre malla
                            return $"{var}_ = linspace({from}, {to}, 101);{nl}{res} = trapz({var}_, arrayfun(@({var}) ({expr}), {var}_));";
                        case Form.LoopSimpson: // Simpson compuesta (n par) por bucle
                            return
                                $"n = 100; h = ({to} - ({from}))/n; {res} = 0;{nl}" +
                                $"for i = 0:n{nl}" +
                                $"    {var} = {from} + i*h;{nl}" +
                                $"    if i == 0 || i == n{nl}" +
                                $"        w = 1;{nl}" +
                                $"    elseif mod(i,2) == 1{nl}" +
                                $"        w = 4;{nl}" +
                                $"    else{nl}" +
                                $"        w = 2;{nl}" +
                                $"    end{nl}" +
                                $"    {res} = {res} + w*({expr});{nl}" +
                                $"end{nl}" +
                                $"{res} = {res}*h/3;";
                        default: // LoopTrapezoid — regla del trapecio por bucle
                            return
                                $"n = 100; h = ({to} - ({from}))/n; {res} = 0;{nl}" +
                                $"for k = 0:n{nl}" +
                                $"    {var} = {from} + k*h;{nl}" +
                                $"    if k == 0 || k == n{nl}" +
                                $"        {res} = {res} + 0.5*({expr});{nl}" +
                                $"    else{nl}" +
                                $"        {res} = {res} + ({expr});{nl}" +
                                $"    end{nl}" +
                                $"end{nl}" +
                                $"{res} = {res}*h;";
                    }
            }
            return "";
        }

        /// <summary>Símbolo matemático del operador (para la notación en la ventana).</summary>
        public static string Symbol(Op op) => op switch
        {
            Op.Sum => "∑",
            Op.Product => "∏",
            Op.Integral => "∫",
            _ => "?"
        };
    }
}
