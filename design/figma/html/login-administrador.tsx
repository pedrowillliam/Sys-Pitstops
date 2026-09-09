const imgRectangle5 = "https://www.figma.com/api/mcp/asset/23eb54f6-37bf-45f4-b212-373803fae5f4.png";
const imgRectangle6 = "https://www.figma.com/api/mcp/asset/e61dbc27-cfab-4234-a6ea-3e91d66241f6.png";

export default function LoginAdministrador() {
  return (
    <div className="relative size-full" data-node-id="80:1295" data-name="Login - ADMINISTRADOR">
      <div className="absolute contents left-0 top-0" data-node-id="11:128" data-name="Login - ADMINISTRADOR">
        <div className="absolute h-[1117px] left-0 rounded-[20px] top-0 w-[1728px]" data-node-id="10:98" style={{ backgroundImage: "linear-gradient(237.2167192935718deg, rgb(255, 255, 255) 25.579%, rgb(148, 163, 184) 100%)" }} />
        <div className="absolute bg-white h-[564px] left-[617px] rounded-[30px] shadow-[0px_5px_5px_5px_rgba(0,0,0,0.25)] top-[277px] w-[500px]" data-node-id="10:99" />
        <div className="absolute bg-[#173676] h-[25px] left-0 top-[1061px] w-[1728px]" data-node-id="10:104" />
        <div className="absolute contents left-[794px] top-[120px]" data-node-id="11:127" data-name="Logo">
          <div className="absolute h-[145.862px] left-[801.78px] top-[120px] w-[131.224px]" data-node-id="10:102">
            <img alt="" className="absolute inset-0 max-w-none object-cover pointer-events-none size-full" src={imgRectangle5} />
          </div>
          <div className="absolute h-[31.034px] left-[794px] top-[238.97px] w-[133.168px]" data-node-id="10:103">
            <img alt="" className="absolute inset-0 max-w-none object-cover pointer-events-none size-full" src={imgRectangle6} />
          </div>
        </div>
        <div className="absolute contents left-[1043px] top-[631px]" data-node-id="10:105" data-name="Quem somos">
          <CodeConnectSnippet data-node-id="10:106" data-name="Circle">
            {/* Simple Design System component */}
            <Circle variantSize="24" size="24" />
          </CodeConnectSnippet>
          <p className="[word-break:break-word] absolute font-['Montserrat_Alternates:SemiBold'] h-[17px] leading-[normal] left-[1053px] not-italic text-[#1e1e1e] text-[16px] top-[633px] w-[3px]" data-node-id="10:107">
            i
          </p>
        </div>
        <div className="absolute contents left-[667px] top-[318px]" data-node-id="11:126" data-name="Email">
          <div className="-translate-x-1/2 absolute bg-white h-[50px] left-[calc(50%+3px)] rounded-[10px] shadow-[0px_5px_5px_5px_rgba(0,0,0,0.25)] top-[348px] w-[400px]" data-node-id="10:108" />
          <p className="[word-break:break-word] absolute font-['Montserrat:SemiBold'] font-semibold h-[23px] leading-[normal] left-[667px] text-[#1e3a8a] text-[20px] top-[318px] w-[155px]" data-node-id="10:111">
            Email/Usuário:
          </p>
          <p className="[text-underline-position:from-font] [word-break:break-word] absolute decoration-from-font decoration-solid font-['Montserrat:Regular'] font-normal h-[19px] leading-[normal] left-[905px] text-[#1e3a8a] text-[16px] top-[413px] underline w-[163px]" data-node-id="10:114">
            Esqueceu o e-mail ?
          </p>
        </div>
        <div className="absolute contents left-[667px] top-[452px]" data-node-id="11:125" data-name="Senha">
          <div className="-translate-x-1/2 -translate-y-1/2 absolute bg-white h-[50px] left-[calc(50%+3px)] rounded-[10px] shadow-[0px_5px_5px_5px_rgba(0,0,0,0.25)] top-[calc(50%-51.5px)] w-[400px]" data-node-id="10:109" />
          <p className="[word-break:break-word] absolute font-['Montserrat:SemiBold'] font-semibold h-[23px] leading-[normal] left-[667px] text-[#1e3a8a] text-[20px] top-[452px] w-[72px]" data-node-id="10:110">
            Senha:
          </p>
          <p className="[word-break:break-word] absolute font-['Montserrat:Regular'] font-normal h-[16px] leading-[normal] left-[673px] text-[#1e1e1e] text-[16px] top-[540px] w-[297px]" data-node-id="10:113">
            Mínimo 8 caracteres.
          </p>
          <p className="[text-underline-position:from-font] [word-break:break-word] absolute decoration-from-font decoration-solid font-['Montserrat:Regular'] font-normal h-[19px] leading-[normal] left-[908px] text-[#1e3a8a] text-[16px] top-[546px] underline w-[163px]" data-node-id="10:115">
            Esqueceu a senha ?
          </p>
        </div>
        <div className="absolute contents left-[667px] top-[633px]" data-node-id="11:124" data-name="Lembre">
          <p className="[word-break:break-word] absolute font-['Montserrat:Regular'] font-normal h-[19px] leading-[normal] left-[697px] text-[16px] text-black top-[633px] w-[144px]" data-node-id="10:116">
            Lembre de mim.
          </p>
          <CodeConnectSnippet data-node-id="10:117" data-name="Square">
            {/* Simple Design System component */}
            <Square variantSize="20" size="20" />
          </CodeConnectSnippet>
        </div>
        <a className="absolute block cursor-pointer h-[80px] left-[617px] top-[761px] w-[500px]" data-node-id="80:1296" data-name="Confirmar">
          <div className="absolute contents left-0 top-0" data-node-id="10:118" data-name="Confirmar">
            <div className="absolute bg-[#173676] h-[80px] left-0 rounded-[20px] shadow-[0px_4px_4px_4px_rgba(0,0,0,0.25)] top-0 w-[500px]" data-node-id="10:119" />
            <div className="absolute bg-[#173676] h-[49px] left-0 top-0 w-[500px]" data-node-id="12:134" />
            <p className="[word-break:break-word] absolute font-['Montserrat:ExtraBold'] font-extrabold h-[28px] leading-[normal] left-[152px] text-[24px] text-left text-white top-[26px] w-[211px]" data-node-id="10:120">
              Acessar Sistema
            </p>
          </div>
        </a>
      </div>
    </div>
  );
}
