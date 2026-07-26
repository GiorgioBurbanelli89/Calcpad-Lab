% symcas via extension compilada (giac) — diferenciacion simbolica nativa
mex('symcas.cpp')                          % en Lab; en Octave: mkoctfile('symcas.cc')
d1 = symcas("diff", "x^3+2*x", "x")        % 3*x^2+2
d2 = symcas("diff", "sin(x)*x^2", "x")
d3 = symcas("diff", "atan(x)", "x")        % 1/(x^2+1)
