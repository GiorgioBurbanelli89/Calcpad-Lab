// GENERADO — no editar a mano.
// Origen: los nombres REALES registrados por el motor de Hekatan Lab, extraidos de
//   hekatan-lab/Symbolic.Core/Matlab/*.cs  con:
//   grep -rho '_builtins\["[A-Za-z0-9_]*"\]' *.cs | sed 's/.*\["//; s/"\]//' | sort -u
// Se excluyen los internos (__hoverdata_lab, __jitstats) y los que ya son
// palabra clave o constante. Regenerar cuando el motor gane funciones.

namespace Calcpad.Wpf;

internal static class MatlabBuiltins
{
    /// <summary>Las 577 funciones que el motor reconoce de verdad.</summary>
    internal static readonly string[] All =
    {
        "abs", "accumarray", "acos", "acosd", "acosh", "acot", "acoth", "acsc", "acsch", "addlistener", "addpath",
        "adjacency", "all", "ancestor", "and", "angle", "annotation", "any", "arrayfun", "asec", "asech", "asin",
        "asind", "asinh", "assert", "assignin", "assume", "assumeAlso", "atan", "atan2", "atan2d", "atand", "atanh",
        "axes", "axis", "bar", "barh", "beta", "bicg", "bin2dec", "binopdf", "blanks", "blkdiag", "bode", "box",
        "brush", "bsxfun", "btdb", "butter", "bvp4c", "c2d", "camlight", "camorbit", "campos", "camproj", "camroll",
        "camtarget", "camup", "camva", "camzoom", "care", "cat", "cat3", "caxis", "cd", "ceil", "cell", "cell2mat",
        "cellfun", "char", "cheby1", "cheby2", "chi2cdf", "chi2pdf", "chol", "circshift", "cla", "clc", "clear",
        "clf", "clim", "close", "coeffs", "collect", "colorbands", "colorbar", "colormap", "colspace", "columns",
        "complex", "cond", "conj", "conncomp", "contains", "contour", "contourf", "conv", "conv2", "cos", "cosd",
        "cosh", "cot", "coth", "cross", "csc", "csch", "csvread", "csvwrite", "cummax", "cummin", "cumprod",
        "cumsum", "cumtrapz", "d2c", "damp", "datacursormode", "dblquad", "dbz", "dcgain", "deblank", "dec2bin",
        "decomposition", "deconv", "deg2rad", "degree", "delaunay", "delaunayTriangulation", "density", "det",
        "diag", "diff", "digraph", "dirac", "disp", "dlmread", "dlmwrite", "dot", "double", "drawnow", "dsolve",
        "eig", "eigenvals", "eigenvecs", "ellip", "endsWith", "eq", "erase", "erf", "erfc", "erfinv", "error",
        "etabs_run", "evalc", "evalin", "exist", "exit", "exp", "expand", "expm", "exportgraphics", "eye", "factor",
        "factorial", "fclose", "fdisp", "feedback", "feof", "feval", "fflush", "fft", "fft2", "fftshift", "fgetl",
        "fgets", "fieldnames", "figure", "fileparts", "fill", "fill3", "filter", "find", "fix", "flip", "fliplr",
        "flipud", "floor", "fminbnd", "fmincon", "fminsearch", "fopen", "format", "fourier", "fpdf", "fprintf",
        "fputs", "freqz", "fscanf", "fsolve", "fspecial", "full", "fullfile", "funm", "fzero", "gamma", "gampdf",
        "gauss_seidel", "gaussint", "gaussint2", "gca", "gcd", "gcf", "ge", "genpath", "get", "getappdata",
        "getframe", "ginput", "gmres", "gradient", "graph", "graphics_toolkit", "grid", "griddata", "gt", "guidata",
        "haspardiso", "heatmap", "heaviside", "hilbert", "hist", "histc", "histcounts", "histogram", "histogram2",
        "hold", "horzcat", "hoverdata", "hypot", "ifft", "ifft2", "ilaplace", "imag", "image", "imagesc",
        "imfilter", "importdata", "impulse", "imread", "imresize", "imshow", "imwrite", "inpolygon", "inputname",
        "int", "int2str", "integral", "integral2", "integral3", "interp1", "interp2", "intersect", "inv", "inverse",
        "ipermute", "isInterior", "isKey", "isUnit", "iscell", "ischar", "iscomplex", "isempty", "isequal",
        "isfield", "isfinite", "isinf", "islogical", "ismember", "isnan", "isnumeric", "isreal", "isscalar",
        "issparse", "isstring", "isstruct", "isvector", "iztrans", "jacobian", "jet", "jsondecode", "jsonencode",
        "keys", "kron", "laplace", "latex", "lcm", "ldivide", "le", "legend", "length", "light", "lighting",
        "limit", "line", "linprog", "linsolve", "linspace", "load", "log", "log10", "log2", "logm", "logspace",
        "lower", "lqe", "lqr", "lsim", "lsqcurvefit", "lsqnonlin", "lt", "lu", "magic", "map", "margin", "mat2str",
        "material", "max", "mean", "median", "mesh", "mex", "mfilename", "min", "minus", "mkdir", "mkoctfile",
        "mldivide", "mod", "movegui", "mtimes", "nchoosek", "ndims", "ne", "neighbors", "nnz", "nonzeros", "norm",
        "normcdf", "norminv", "normpdf", "not", "nthroot", "null", "num2str", "numedges", "numel", "numnodes",
        "nyquist", "ode23", "ode4", "ode45", "ode_euler", "ones", "ones3", "or", "orth", "pad", "pan", "parallel",
        "patch", "pause", "pcg", "pchip", "pcolor", "pdepe", "peaks", "permute", "piecewise", "pinv", "plot",
        "plot3", "plus", "plus_str", "poisspdf", "polar", "pole", "poly", "poly2sym", "polyder", "polyfit",
        "polyint", "polyval", "polyvalm", "power", "prctile", "pretty", "primes", "print", "printf", "prod", "puts",
        "pwd", "qr", "quad", "quadgk", "quadl", "quadprog", "quit", "quiver", "quiver3", "rad2deg", "rand", "randi",
        "randn", "randperm", "rank", "rdivide", "readmatrix", "readtable", "real", "rectpuls", "regexp", "regexpi",
        "regexprep", "rem", "remove", "repelem", "repmat", "reshape", "rgb2gray", "rlocus", "rmpath", "rng",
        "roots", "rot90", "rotate3d", "round", "rows", "rowspace", "save", "saveas", "scatter", "scatter3", "schur",
        "sec", "sech", "separateUnits", "series", "set", "setappdata", "setdiff", "sgtitle", "shading",
        "shortestpath", "sign", "simplify", "sin", "sinc", "sind", "sinh", "size", "slabview3d", "slice",
        "solidmesh", "solve", "sort", "sortrows", "sparse", "spdiags", "speye", "spline", "spones", "sprintf",
        "spsolve", "spy", "sqrt", "sqrtm", "squeeze", "ss", "ss2tf", "startsWith", "std", "stem", "step",
        "stepinfo", "str2double", "str2num", "strcat", "strcmp", "strcmpi", "streamslice", "strfind", "string",
        "strjoin", "strlen", "strlength", "strncmp", "strncmpi", "strrep", "strsplit", "strtrim", "struct",
        "structfun", "sub2ind", "subplot", "subs", "sum", "surf", "svd", "sym", "sym2poly", "syms", "symsum",
        "symunit", "tabulate", "tan", "tand", "tanh", "taylor", "tcdf", "tetramesh", "text", "textread", "tf",
        "tf2ss", "tic", "times", "title", "toc", "toeplitz", "tpdf", "trace", "transpose", "trapz", "trigexpand",
        "trigsimplify", "tril", "trimesh", "triplequad", "triplot", "trisurf", "triu", "trunc", "uicontrol",
        "uipanel", "uminus", "union", "unique", "unitConvert", "uplus", "upper", "values", "vander", "var",
        "vertcat", "view", "warning", "who", "whos", "writematrix", "writetable", "xcorr", "xcov", "xlabel", "xlim",
        "xlsread", "xlswrite", "xor", "ylabel", "ylim", "zero", "zeros", "zeros3", "zlabel", "zlim", "zoom", "zpk",
        "ztrans",
    };

    internal static readonly string[] Keywords =
    {
        "function", "end", "endfunction", "for", "parfor", "while", "endwhile", "if", "elseif", "else", "endif",
        "switch", "case", "otherwise", "endswitch", "break", "continue", "return", "try", "catch", "end_try_catch",
        "do", "until", "global", "persistent", "classdef", "properties", "methods", "events", "enumeration",
        "arguments", "spmd", "nargin", "nargout", "varargin", "varargout",
    };

    internal static readonly string[] Constants =
    {
        "true", "false", "pi", "Inf", "inf", "NaN", "nan", "NaT", "eps", "realmax", "realmin", "Intmax", "intmin",
    };
}
