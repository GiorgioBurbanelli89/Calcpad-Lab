function [a, b, c] = triple_out(x)
a = x;
b = x * 2;
c = x * 3;
end

% Test directo
[p, q, r] = triple_out(10);
fprintf('p=%g q=%g r=%g\n', p, q, r);
