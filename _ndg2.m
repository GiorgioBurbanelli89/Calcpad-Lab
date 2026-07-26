[Xg,Yg,Zg]=ndgrid(0:0.5:1, 0:0.5:1, 0:0.5:1);
P=[Xg(:) Yg(:) Zg(:)];
disp('size P:'); disp(size(P));       % debe 27 3
disp('primeras 3 filas:'); disp(P(1:3,:));
disp('sz Xg (3D):'); disp(size(Xg));  % debe 3 3 3
