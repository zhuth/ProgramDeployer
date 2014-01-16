ProgramDeployer
===============

检疫的程序分发工具。在本地（分发者）运行服务器，在被分发的机器上运行客户端。默认监听20023号端口，请在防火墙中设置好。
基于简易的 HTTP + PUSH 协议。

部署脚本样例：

		client http://XXXXXXXXX:20023/ http://YYYYYYYYYY:20023/ # 指定一个或多个客户端
		proc kill MediaImportTool # 关闭指定名称的所有进程
		wait 1000 # 等候 1 秒
		map alias/ D:\Studio\XXXX\bin\Debug # 将本地的文件夹部署到客户端
		map alias/MediaImportTool.exe.config D:\Studio\XXXXXX\MediaImportTool\bin\Release\MediaImportTool.exe.config # 将本地的某个文件部署到客户端
		proc start D:\MediaTest\run.bat # 启动指定的程序

其中客户端的路径别名 alias 在 ProgramDeployerClient.config 的设置中给定，格式如下：

		<setting name="MonitoringDirectories" serializeAs="String">
			<value>self=.;D=D:\;</value>
		</setting>

即：别名=路径，中间用;分割。
