// Creates uniquely named test databases; the fixtures verify those names before cleanup.
const {spawnSync}=require('node:child_process'),path=require('node:path');
const api=path.resolve(__dirname,'../so.api');
function run(args,env={}){const result=spawnSync('dotnet',args,{cwd:api,windowsHide:true,stdio:'inherit',env:{...process.env,...env,Mail__Enabled:"false",Mail__Host:"",Mail__Password:""}});if(result.error)throw result.error;if(result.status!==0)process.exit(result.status||1);}
for(const name of ['CommerceChecks','OwnerStateChecks','CustomerIdentityChecks','InventoryChecks']){
 const project=path.join(__dirname,name,name+'.csproj');
 run(['build',project,'-c','Release','-p:UseAppHost=false']);
 run([path.join(__dirname,name,'bin/Release/net8.0',name+'.dll')],{SO_TEST_API_DLL:path.join(api,'bin/Release/net8.0/so.api.dll'),SO_BROWSER_CHECKS:'0'});
}
