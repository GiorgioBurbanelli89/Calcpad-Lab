%% DEMO: Todos los tipos de gráfico del motor MATLAB puro

%% ===== 1. plot — línea 2D =====
x = linspace(0, 2*pi, 100)
y = sin(x)
y2 = cos(x)
figure
plot(x, y, 'b-', x, y2, 'r--')
xlabel('x')
ylabel('y')
title('1. plot — sin(x) y cos(x)')
legend('sin', 'cos')
grid on

%% ===== 2. scatter — puntos 2D =====
n = 50
xs = rand(1, n) * 10
ys = xs + randn(1, n) * 2
figure
scatter(xs, ys)
xlabel('x')
ylabel('y')
title('2. scatter — nube de puntos con tendencia lineal')
grid on

%% ===== 3. bar — barras verticales =====
cats = 1:6
heights = [23, 45, 56, 78, 32, 12]
figure
bar(cats, heights)
xlabel('Categoría')
ylabel('Valor')
title('3. bar — barras verticales')

%% ===== 4. barh — barras horizontales =====
figure
barh(cats, heights)
xlabel('Valor')
ylabel('Categoría')
title('4. barh — barras horizontales')

%% ===== 5. stem — gráfico de tallos (discreto) =====
n_s = 20
xs_s = 1:n_s
ys_s = sin(xs_s * 0.5)
figure
stem(xs_s, ys_s)
xlabel('n')
ylabel('y[n]')
title('5. stem — señal discreta sin(0.5n)')

%% ===== 6. polar — coordenadas polares =====
theta = linspace(0, 2*pi, 200)
r = 1 + 0.5 * cos(4 * theta)
figure
polar(theta, r)
title('6. polar — rosa de 4 pétalos: r = 1 + 0.5·cos(4θ)')

%% ===== 7. histogram — histograma 1D =====
data = randn(1, 1000)
figure
histogram(data, 30)
xlabel('valor')
ylabel('frecuencia')
title('7. histogram — distribución normal N(0,1), 1000 muestras')

%% ===== 8. histogram2 — histograma 2D =====
n2 = 500
xx2 = randn(1, n2)
yy2 = randn(1, n2) * 0.8 + xx2 * 0.5
figure
histogram2(xx2, yy2)
xlabel('x')
ylabel('y')
title('8. histogram2 — distribución bivariada correlacionada')

%% ===== 9. heatmap — mapa de calor matriz 2D =====
M = magic(8)
figure
heatmap(M)
title('9. heatmap — matriz mágica 8×8')

%% ===== 10. contour — curvas de nivel =====
xx = linspace(-3, 3, 50)
yy = linspace(-3, 3, 50)
[X, Y] = meshgrid(xx, yy)
Z = exp(-(X.^2 + Y.^2)) - exp(-((X-1).^2 + (Y-1).^2))
figure
contour(X, Y, Z)
xlabel('x')
ylabel('y')
title('10. contour — curvas de nivel de dos gaussianas')

%% ===== 11. contourf — contornos rellenados =====
figure
contourf(X, Y, Z)
xlabel('x')
ylabel('y')
title('11. contourf — contornos rellenados')

%% ===== 12. imagesc — matriz como imagen escalada =====
figure
imagesc(M)
xlabel('columna')
ylabel('fila')
title('12. imagesc — matriz como imagen')

%% ===== 13. spy — patrón de sparse =====
% spy muestra ubicación de elementos no-nulos
S = [1 0 0 5; 0 2 0 0; 0 0 3 0; 6 0 0 4]
figure
spy(S)
title('13. spy — patrón sparsity 4×4')

%% ===== 14. quiver — campo vectorial 2D =====
xq = linspace(-2, 2, 15)
yq = linspace(-2, 2, 15)
[XQ, YQ] = meshgrid(xq, yq)
% Campo rotacional
UQ = -YQ
VQ = XQ
figure
quiver(XQ, YQ, UQ, VQ)
xlabel('x')
ylabel('y')
title('14. quiver — campo vectorial rotacional')
axis equal

%% ===== 15. plot3 — curva 3D (hélice) =====
t3 = linspace(0, 6*pi, 200)
x3 = cos(t3)
y3 = sin(t3)
z3 = t3 / (2*pi)
figure
plot3(x3, y3, z3)
xlabel('x')
ylabel('y')
zlabel('z')
title('15. plot3 — hélice 3D')
grid on

%% ===== 16. scatter3 — puntos 3D =====
n3 = 100
xs3 = randn(1, n3)
ys3 = randn(1, n3)
zs3 = xs3.^2 + ys3.^2 + randn(1, n3) * 0.1
figure
scatter3(xs3, ys3, zs3)
xlabel('x')
ylabel('y')
zlabel('z')
title('16. scatter3 — paraboloide con ruido')

%% ===== 17. surf — superficie 3D =====
figure
surf(X, Y, Z)
xlabel('x')
ylabel('y')
zlabel('z')
title('17. surf — superficie 3D de dos gaussianas')

%% ===== 18. mesh — wireframe 3D =====
figure
mesh(X, Y, Z)
xlabel('x')
ylabel('y')
zlabel('z')
title('18. mesh — wireframe')

%% ===== 19. quiver3 — campo vectorial 3D (vórtice) =====
% Construyo manualmente la grilla 3D 5×5×5
N3 = 5
Xv = zeros(N3, N3*N3)
Yv = zeros(N3, N3*N3)
Zv = zeros(N3, N3*N3)
Uv = zeros(N3, N3*N3)
Vv = zeros(N3, N3*N3)
Wv = zeros(N3, N3*N3)
idx = 1
for k = 1:N3
    zk = -1 + (k-1) * 0.5
    for j = 1:N3
        yk = -1 + (j-1) * 0.5
        for i = 1:N3
            xk = -1 + (i-1) * 0.5
            Xv(i, idx) = xk
            Yv(i, idx) = yk
            Zv(i, idx) = zk
            Uv(i, idx) = -yk
            Vv(i, idx) = xk
            Wv(i, idx) = 0
        end
        idx = idx + 1
    end
end
figure
quiver3(Xv, Yv, Zv, Uv, Vv, Wv)
xlabel('x')
ylabel('y')
zlabel('z')
title('19. quiver3 — vórtice cilíndrico')

%% ===== Fin =====
disp('Todos los plots generados ✓')
