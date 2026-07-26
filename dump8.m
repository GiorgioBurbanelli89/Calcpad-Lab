kref=8; frame_fiber;
M=[X Ug(1:6:end) Ug(2:6:end) Ug(3:6:end)];   % x y z ux uy uz
dlmwrite('frame_m8.csv',M,'precision','%.8e'); disp('ok');
