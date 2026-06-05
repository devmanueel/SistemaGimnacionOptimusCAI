Plan de Modificación: Integración de Envío Masivo en "Nuevo Mensaje"Este plan detalla los cambios estructurales y visuales para transformar la ventana de creación actual en un asistente unificado de mensajería (Individual y Masivo), manteniendo la compatibilidad con tus controladores, DAOs y modelos existentes.📋 Fase 1: Redefinición del Flujo y Selector de ModoEn la sección principal de WhatsApp, al presionar el botón NUEVO MENSAJE, se abrirá la ventana emergente habitual. La principal novedad es que la parte superior de esta ventana incluirá ahora un selector de tipo de envío (RadioButtons con estilo de botón moderno) para conmutar entre los dos modos.Modo Individual (Por defecto): Muestra el diseño original de la sección de Nuevo Mensaje (Selector de un solo socio, campo de teléfono y plantillas rápidas).Modo Masivo: Oculta los campos individuales y despliega dinámicamente el panel de selección múltiple con filtros y checkboxes.🛠️ Fase 2: Ajustes en el XAML de la Sección de Nuevo Mensaje (NuevoMensaje.xaml)Modificaremos el contenedor principal agregando el selector de modo en la cabecera y usando contenedores con nombres claros (GridIndividual y GridMasivo) para alternar su visibilidad (Visibility) mediante bindings o desde el code-behind.Código XAML Estructurado para el Nuevo Formulario Unificado:XML<!-- Cabecera del Modal con Selector de Modo -->
<StackPanel Margin="0,0,0,15">
    <TextBlock Text="TIPO DE ENVÍO" Foreground="#777777" FontSize="12" FontWeight="Bold" Margin="0,0,0,5"/>
    <StackPanel Orientation="Horizontal">
        <RadioButton x:Name="RbIndividual" Content="Individual" IsChecked="True" 
                     Style="{StaticResource ToggleButtonStyle}" Checked="Mode_Checked" Margin="0,0,10,0"/>
        <RadioButton x:Name="RbMasivo" Content="Masivo (En Bloque)" 
                     Style="{StaticResource ToggleButtonStyle}" Checked="Mode_Checked"/>
    </StackPanel>
</StackPanel>

<Grid>
    <!-- ========================================== -->
    <!-- MODO INDIVIDUAL (Campos Existentes)        -->
    <!-- ========================================== -->
    <StackPanel x:Name="GridIndividual" Visibility="Visible">
        <TextBlock Text="SOCIO (opcional, pone número externo si está vacío)" Foreground="#777777" FontSize="12" Margin="0,0,0,5"/>
        <!-- ComboBox y TextBox de teléfono originales de la sección de Nuevo Mensaje -->
    </StackPanel>

    <!-- ========================================== -->
    <!-- MODO MASIVO (Nueva Funcionalidad)          -->
    <!-- ========================================== -->
    <StackPanel x:Name="GridMasivo" Visibility="Collapsed">
        <!-- Botones de Acción Rápida -->
        <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
            <Button x:Name="BtnMarcarActivos" Content="SELECCIONAR ACTIVOS" Style="{StaticResource BotonVerdeStyle}" Click="BtnMarcarActivos_Click" Margin="0,0,10,0"/>
            <Button x:Name="BtnDesmarcarTodos" Content="DESMARCAR TODOS" Style="{StaticResource BotonRojoStyle}" Click="BtnDesmarcarTodos_Click"/>
        </StackPanel>

        <!-- Buscador Interno de Socios -->
        <Border Background="#141916" CornerRadius="5" BorderBrush="#27382B" BorderThickness="1" Margin="0,0,0,10" Padding="8,5">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="🔍" Foreground="#777777" Margin="5,0,10,0" VerticalAlignment="Center"/>
                <TextBox x:Name="TxtBuscarSocioMasivo" Grid.Column="1" Background="Transparent" BorderThickness="0" Foreground="White" TextChanged="TxtBuscarSocioMasivo_TextChanged"/>
            </Grid>
        </Border>

        <!-- Lista de Socios con Checkboxes -->
        <Border Background="#141916" BorderBrush="#27382B" BorderThickness="1" CornerRadius="5" Height="180">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <ListView x:Name="ListViewSociosMasivo" Background="Transparent" BorderThickness="0" Foreground="White">
                    <ListView.ItemTemplate>
                        <DataTemplate>
                            <Grid Padding="10,5">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,15,0"/>
                                <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding NombreCompleto}" Foreground="White" FontWeight="SemiBold"/>
                                    <TextBlock Text="{Binding Telefono}" Foreground="#555555" Margin="15,0,0,0"/>
                                </StackPanel>
                                <Border Grid.Column="2" Background="#1C2D22" CornerRadius="3" Padding="6,2">
                                    <TextBlock Text="{Binding EstadoMembresia}" Foreground="#2ECC71" FontSize="10" FontWeight="Bold"/>
                                </Border>
                            </Grid>
                        </DataTemplate>
                    </ListView.ItemTemplate>
                </ListView>
            </ScrollViewer>
        </Border>
    </StackPanel>
</Grid>
🔄 Fase 3: Lógica de Intercambio de Modos (Code-Behind)Para alternar de forma limpia las secciones visuales sin romper las asignaciones de datos, el evento Mode_Checked controlará la propiedad de visibilidad en el archivo .xaml.cs:C#private void Mode_Checked(object sender, RoutedEventArgs e)
{
    if (GridIndividual == null || GridMasivo == null) return;

    if (RbIndividual.IsChecked == true)
    {
        GridIndividual.Visibility = Visibility.Visible;
        GridMasivo.Visibility = Visibility.Collapsed;
    }
    else if (RbMasivo.IsChecked == true)
    {
        GridIndividual.Visibility = Visibility.Collapsed;
        GridMasivo.Visibility = Visibility.Visible;
        
        // Cargar la lista de socios desde el controlador solo si está vacía
        if (ListViewSociosMasivo.ItemsSource == null) {
            CargarSociosParaMasivo();
        }
    }
}

📈 Matriz de Comportamiento del Botón "GUARDAR"El botón GUARDAR de la ventana mutará su comportamiento lógico analizando cuál de los dos RadioButtons se encuentra activo en el momento del clic:
Modo ActivoValidación NecesariaComportamiento del Controlador / DAOIndividualRequiere socio seleccionado o un número telefónico válido ingresado a mano.Sigue la ruta actual: guarda un único registro en la base de datos vinculado al socio específico.MasivoVerifica que el cuadro de texto del mensaje no esté vacío y que exista al menos un socio marcado con el Checkbox.Toma el texto del mensaje base, recorre los socios tildados (IsSelected == true) y procesa una inserción masiva a través del DAO, generando un registro individual en estado Pendiente por cada destinatario.