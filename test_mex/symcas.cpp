// symcas.cpp - CAS simbolico via extension MEX compilada (giac) para Hekatan Lab.
// Firma string de la ABI: const char* hkmex_str(int nin, const char* const* in).
// Uso: symcas("diff", expr, var) -> derivada como string. Enlaza contra giac.dll.
#include <string>

// Prototipo del simbolo real de giac (Itanium: _ZN4giac7casevalEPKc).
namespace giac { const char* caseval(const char* s); }

extern "C" {

__declspec(dllexport) const char* hkmex_str(int nin, const char* const* in)
{
    static std::string result;   // static: el buffer sobrevive al return (const char* al host)
    if (nin < 1) { result = ""; return result.c_str(); }

    std::string op   = in[0];
    std::string expr = nin > 1 ? in[1] : "";
    std::string var  = nin > 2 ? in[2] : "x";

    std::string cmd;
    if (op == "diff")                         cmd = "diff(" + expr + "," + var + ")";
    else if (op == "int" || op == "integrate") cmd = "integrate(" + expr + "," + var + ")";
    else if (op == "simplify")                cmd = "simplify(" + expr + ")";
    else if (op == "solve")                   cmd = "solve(" + expr + "," + var + ")";
    else                                      cmd = op + "(" + expr + ")";  // puerta abierta

    const char* r = giac::caseval(cmd.c_str());
    result = (r != nullptr) ? r : "";
    return result.c_str();
}

} // extern "C"
