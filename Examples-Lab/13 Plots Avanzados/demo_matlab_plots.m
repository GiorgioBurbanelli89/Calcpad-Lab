%% DEMO: Todos los tipos de gráfico — MATLAB real (MathWorks)
% Cada figura se guarda como PNG en demo_matlab_figs/
close all
mkdir('demo_matlab_figs');

%% 1. plot — línea 2D
x = linspace(0, 2*pi, 100);
figure; plot(x, sin(x), 'b-', x, cos(x), 'r--', 'LineWidth', 1.5);
xlabel('x'); ylabel('y'); title('1. plot — sin(x) y cos(x)');
legend('sin','cos'); grid on;
saveas(gcf, 'demo_matlab_figs/01_plot.png');

%% 2. scatter — puntos 2D
n = 50; xs = rand(1,n)*10; ys = xs + randn(1,n)*2;
figure; scatter(xs, ys, 40, 'filled');
xlabel('x'); ylabel('y'); title('2. scatter'); grid on;
saveas(gcf, 'demo_matlab_figs/02_scatter.png');

%% 3. bar — barras verticales
figure; bar(1:6, [23 45 56 78 32 12]);
xlabel('Categoría'); ylabel('Valor'); title('3. bar');
saveas(gcf, 'demo_matlab_figs/03_bar.png');

%% 4. barh — barras horizontales
figure; barh(1:6, [23 45 56 78 32 12]);
xlabel('Valor'); ylabel('Categoría'); title('4. barh');
saveas(gcf, 'demo_matlab_figs/04_barh.png');

%% 5. stem — tallos discretos
figure; stem(1:20, sin((1:20)*0.5), 'filled');
xlabel('n'); ylabel('y[n]'); title('5. stem — sin(0.5n)');
saveas(gcf, 'demo_matlab_figs/05_stem.png');

%% 6. polar — rosa de 4 pétalos
theta = linspace(0, 2*pi, 200); r = 1 + 0.5*cos(4*theta);
figure; polar(theta, r);
title('6. polar — r = 1 + 0.5·cos(4θ)');
saveas(gcf, 'demo_matlab_figs/06_polar.png');

%% 7. histogram — normal(0,1)
figure; histogram(randn(1,1000), 30);
xlabel('valor'); ylabel('freq'); title('7. histogram — N(0,1), 1000 muestras');
saveas(gcf, 'demo_matlab_figs/07_histogram.png');

%% 8. histogram2 — bivariado correlacionado
n2 = 500; xx2 = randn(1,n2); yy2 = randn(1,n2)*0.8 + xx2*0.5;
figure; histogram2(xx2, yy2, 'DisplayStyle','tile');
xlabel('x'); ylabel('y'); title('8. histogram2');
saveas(gcf, 'demo_matlab_figs/08_histogram2.png');

%% 9. heatmap — matriz mágica
figure; imagesc(magic(8)); colorbar;
title('9. heatmap (imagesc) — magic(8)');
saveas(gcf, 'demo_matlab_figs/09_heatmap.png');

%% 10. contour — curvas de nivel
[X,Y] = meshgrid(linspace(-3,3,50), linspace(-3,3,50));
Z = exp(-(X.^2+Y.^2)) - exp(-((X-1).^2+(Y-1).^2));
figure; contour(X, Y, Z, 15);
xlabel('x'); ylabel('y'); title('10. contour — dos gaussianas');
saveas(gcf, 'demo_matlab_figs/10_contour.png');

%% 11. contourf — contornos rellenos
figure; contourf(X, Y, Z, 15); colorbar;
xlabel('x'); ylabel('y'); title('11. contourf');
saveas(gcf, 'demo_matlab_figs/11_contourf.png');

%% 12. imagesc — matriz como imagen
figure; imagesc(Z); colorbar;
xlabel('col'); ylabel('fila'); title('12. imagesc');
saveas(gcf, 'demo_matlab_figs/12_imagesc.png');

%% 13. spy — patrón de sparsity
S = sparse([1 0 0 5; 0 2 0 0; 0 0 3 0; 6 0 0 4]);
figure; spy(S);
title('13. spy — pattern 4×4');
saveas(gcf, 'demo_matlab_figs/13_spy.png');

%% 14. quiver — campo vectorial rotacional 2D
[XQ,YQ] = meshgrid(linspace(-2,2,15));
figure; quiver(XQ, YQ, -YQ, XQ);
xlabel('x'); ylabel('y'); title('14. quiver — rotacional'); axis equal;
saveas(gcf, 'demo_matlab_figs/14_quiver.png');

%% 15. plot3 — hélice 3D
t3 = linspace(0, 6*pi, 200);
figure; plot3(cos(t3), sin(t3), t3/(2*pi), 'LineWidth', 1.5);
xlabel('x'); ylabel('y'); zlabel('z'); title('15. plot3 — hélice'); grid on;
saveas(gcf, 'demo_matlab_figs/15_plot3.png');

%% 16. scatter3 — paraboloide con ruido
n3 = 100; xs3 = randn(1,n3); ys3 = randn(1,n3);
zs3 = xs3.^2 + ys3.^2 + randn(1,n3)*0.1;
figure; scatter3(xs3, ys3, zs3, 30, zs3, 'filled');
xlabel('x'); ylabel('y'); zlabel('z'); title('16. scatter3 — paraboloide');
saveas(gcf, 'demo_matlab_figs/16_scatter3.png');

%% 17. surf — superficie 3D
figure; surf(X, Y, Z);
xlabel('x'); ylabel('y'); zlabel('z'); title('17. surf — dos gaussianas');
shading interp; colorbar;
saveas(gcf, 'demo_matlab_figs/17_surf.png');

%% 18. mesh — wireframe
figure; mesh(X, Y, Z);
xlabel('x'); ylabel('y'); zlabel('z'); title('18. mesh — wireframe');
saveas(gcf, 'demo_matlab_figs/18_mesh.png');

%% 19. quiver3 — vórtice 3D
[Xv,Yv,Zv] = meshgrid(-1:0.5:1, -1:0.5:1, -1:0.5:1);
figure; quiver3(Xv, Yv, Zv, -Yv, Xv, zeros(size(Zv)));
xlabel('x'); ylabel('y'); zlabel('z'); title('19. quiver3 — vórtice');
saveas(gcf, 'demo_matlab_figs/19_quiver3.png');

%% 20. pie — gráfico de torta
figure; pie([30 20 25 15 10], {'A','B','C','D','E'});
title('20. pie');
saveas(gcf, 'demo_matlab_figs/20_pie.png');

%% 21. area — área rellenada
xa = 1:10; ya = [xa; xa*0.7; xa*0.4]';
figure; area(xa, ya);
xlabel('x'); ylabel('y'); title('21. area — stacked'); legend('a','b','c');
saveas(gcf, 'demo_matlab_figs/21_area.png');

%% 22. errorbar — barras de error
xe = 1:10; ye = sin(xe); ee = 0.2*ones(size(xe));
figure; errorbar(xe, ye, ee, 'o-');
xlabel('x'); ylabel('y'); title('22. errorbar'); grid on;
saveas(gcf, 'demo_matlab_figs/22_errorbar.png');

%% 23. boxplot — diagrama de caja
data_bp = [randn(50,1); randn(50,1)+2; randn(50,1)-1];
groups = [ones(50,1); 2*ones(50,1); 3*ones(50,1)];
figure; boxplot(data_bp, groups);
xlabel('grupo'); ylabel('valor'); title('23. boxplot');
saveas(gcf, 'demo_matlab_figs/23_boxplot.png');

%% 24. semilogy — escala log en y
xl = 1:50; yl = exp(xl*0.2);
figure; semilogy(xl, yl, 'b-', 'LineWidth', 1.5);
xlabel('x'); ylabel('y (log)'); title('24. semilogy — exp(0.2x)'); grid on;
saveas(gcf, 'demo_matlab_figs/24_semilogy.png');

%% 25. loglog — ambos ejes log
xll = logspace(0, 3, 50); yll = xll.^2.5;
figure; loglog(xll, yll, 'r-', 'LineWidth', 1.5);
xlabel('x (log)'); ylabel('y (log)'); title('25. loglog — y = x^{2.5}'); grid on;
saveas(gcf, 'demo_matlab_figs/25_loglog.png');

%% 26. fill — polígono cerrado
xf = [0 1 1 0]; yf = [0 0 1 1];
figure; fill(xf, yf, 'c');
xlabel('x'); ylabel('y'); title('26. fill — cuadrado'); axis equal;
saveas(gcf, 'demo_matlab_figs/26_fill.png');

%% 27. fplot — función simbólica
figure; fplot(@(x) x.*sin(1./x), [-1 1]);
xlabel('x'); ylabel('y'); title('27. fplot — x·sin(1/x)'); grid on;
saveas(gcf, 'demo_matlab_figs/27_fplot.png');

disp('Todos los plots generados ✓ — figuras en demo_matlab_figs/');
exit;
