clear; clc;
syms x y a b c
f = x^3 + 2*x^2 - 5*x + 7;
df = diff(f, x);
fprintf('f(x) = %s\n', char(f));
fprintf('df/dx = %s\n', char(df));
g = x^3 + 2*x;
fprintf('int %s dx = %s\n', char(g), char(int(g, x)));
fprintf('lim x->0 de sin(x)/x = %s\n', char(limit(sin(x)/x, x, 0)));
M = [a b; c x];
fprintf('det([a b; c x]) = %s\n', char(det(M)));
h = x^2 + 3*x;
fprintf('Para h = %s : h''(x) = %s, int h dx = %s, h(2) = %g\n', char(h), char(diff(h)), char(int(h)), double(subs(h, x, 2)));
