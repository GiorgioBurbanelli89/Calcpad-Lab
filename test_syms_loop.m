% Test: acumulacion simbolica en un loop (syms fuera de %)
syms x
y = x;
for i = 1:5
    y = y + i;
end
y
