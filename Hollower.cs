using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Seion.BatchHollower
{
    public class Hollower
    {
        public AssemblyDefinition GetAssemblyDefinition(string inputPath, string inputFile)
        {
            var resolver = new DefaultAssemblyResolver();
            var parameters = new ReaderParameters();

            resolver.AddSearchDirectory(inputPath);            
            parameters.AssemblyResolver = resolver;

            return AssemblyDefinition.ReadAssembly(inputFile, parameters);
        }

        public void HollowAssembly(string inputPath, string inputFile, string outputFile)
        {
            using (var assembly = GetAssemblyDefinition(inputPath, inputFile))
            {
                var types = assembly.MainModule.GetAllTypes();

                foreach (var type in types)
                {
                    HollowType(type);
                }

                assembly.Write(outputFile);
            }
        }

        public void HollowType(TypeDefinition type)
        {
            HollowMethods(type);
            HollowProperties(type);
        }

        public void HollowMethods(TypeDefinition type)
        {
            if (type == null)
            {
                return;
            }

            if (!type.HasMethods)
            {
                return;
            }

            foreach (var method in type.Methods)
            {
                HollowMethod(method);
            }
        }

        public void HollowProperties(TypeDefinition type)
        {
            if (type == null)
            {
                return;
            }

            if (!type.HasProperties)
            {
                return;
            }

            foreach (var property in type.Properties)
            {
                HollowMethod(property.GetMethod);
                HollowMethod(property.SetMethod);
            }
        }

        public void HollowMethod(MethodDefinition method)
        {
            if (method == null)
                return;

            if (!method.HasBody)
                method.Body = new Mono.Cecil.Cil.MethodBody(method);

            // Make sure method is not extern
            method.IsRuntime = false;
            method.IsInternalCall = false;
            method.ImplAttributes &= ~MethodImplAttributes.InternalCall;

            var il = method.Body.GetILProcessor();
            method.Body.Instructions.Clear();
            method.Body.Variables.Clear();

            // Insert a default return depending on return type
            if (method.ReturnType.FullName == "System.Void")
            {
                // void --> just return
                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));
            }
            else
            {
                // non-void --> load default(T) then return

                var returnType = method.ReturnType;

                if (returnType.IsValueType || returnType.IsGenericParameter)
                {
                    // Create a local variable of return type
                    var varDef = new Mono.Cecil.Cil.VariableDefinition(returnType);
                    method.Body.Variables.Add(varDef);

                    il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldloca, varDef));
                    il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Initobj, returnType));
                    il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldloc, varDef));
                }
                else
                {
                    // reference type --> load null
                    il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldnull));
                }

                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));
            }
        }

    }
}